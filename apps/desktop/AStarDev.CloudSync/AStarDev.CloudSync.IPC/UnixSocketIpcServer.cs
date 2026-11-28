using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.IPC;

/// <summary>Serves the IPC contract over a Unix domain socket, one length-prefixed JSON frame per envelope.</summary>
/// <param name="socketPath">Where the socket file is created.</param>
/// <param name="onError">Told about failures nobody else can receive: a broken peer, a dying accept loop.</param>
/// <param name="logger">Receives connection and failed-request logs; omit to log nothing.</param>
internal sealed class UnixSocketIpcServer(string socketPath, Action<IpcError>? onError = null, ILogger? logger = null) : IIpcServer
{
    private delegate Task<Result<object, IpcError>> Handler(object payload, CancellationToken cancellationToken);

    private sealed class Connection(EnvelopeChannel channel)
    {
        public EnvelopeChannel Channel { get; } = channel;

        public volatile bool Subscribed;
    }

    private readonly ConcurrentDictionary<string, Handler> commands = new();
    private readonly ConcurrentDictionary<string, Handler> queries = new();
    private readonly ConcurrentDictionary<Connection, byte> connections = new();
    private readonly Action<IpcError> report = onError ?? (_ => { });
    private readonly ILogger log = logger ?? NullLogger.Instance;
    private CancellationTokenSource? lifetime;
    private Task<Result<Unit, IpcError>> serverLoop = Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    private int disposed;

    /// <inheritdoc/>
    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand
        => commands[typeof(TCommand).Name] = (payload, cancellationToken) => RunAsync(payload, handler, cancellationToken);

    /// <inheritdoc/>
    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult>
        => queries[typeof(TQuery).Name] = (payload, cancellationToken) => RunAsync(payload, handler, cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, IpcError>> StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<Result<Unit, IpcError>>(cancellationToken);

        if (lifetime is not null)
            return Task.FromResult<Result<Unit, IpcError>>(new Fail<IpcError>(new ListenFailed("The IPC server is already started.")));

        return Task.FromResult<Result<Unit, IpcError>>(OpenListener() switch
        {
            Ok<Socket> listener => Launch(listener.Value, cancellationToken),
            Fail<IpcError> fail => fail
        });
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, IpcError>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = IpcTelemetry.StartEventPublish(typeof(TEvent).Name);

        return await PushAsync(IpcTelemetry.Inject(new EventEnvelope(typeof(TEvent).Name, @event), activity), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, IpcError>> StopAsync(CancellationToken cancellationToken)
    {
        if (lifetime is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new Ok<Unit>(default);
        }

        await lifetime.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();

        await Task.WhenAny(serverLoop).WaitAsync(cancellationToken);

        return serverLoop.IsCompletedSuccessfully ? serverLoop.Result : new Ok<Unit>(default);
    }

    /// <summary>Stops the server as <see cref="StopAsync"/> does, without a way to cancel the wait. Safe to call more than once.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        _ = await StopAsync(CancellationToken.None);
        lifetime?.Dispose();
    }

    private static async Task<Result<object, IpcError>> RunAsync<TRequest, TResponse>(object payload, Func<TRequest, CancellationToken, Task<Result<TResponse, IpcError>>> handler, CancellationToken cancellationToken)
        => IpcJson.FromElement<TRequest>(payload) switch
        {
            Ok<TRequest> request => await handler(request.Value, cancellationToken) switch
            {
                Ok<TResponse> response => new Ok<object>((object?)response.Value ?? IpcJson.NullPayload),
                Fail<IpcError> handlerFailure => handlerFailure
            },
            Fail<IpcError> decodeFailure => decodeFailure
        };

    private Result<Socket, IpcError> OpenListener()
    {
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var opened = Try.Run(() =>
        {
            CleanupStaleState();
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            listener.Listen();
            return listener;
        });

        if (opened is Failure<Socket>)
        {
            listener.Dispose();
        }

        return opened.ToResult(IpcErrors.AsListenFailed);
    }

    private void CleanupStaleState()
    {
        EnsureSocketDirectory();
        File.Delete(socketPath);
    }

    private void EnsureSocketDirectory()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(socketPath));
        if (string.IsNullOrEmpty(directory) || Directory.Exists(directory)) return;

        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(directory);
        else
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private Result<Unit, IpcError> Launch(Socket listener, CancellationToken cancellationToken)
    {
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        serverLoop = ServeAsync(listener, lifetime.Token);
        return new Ok<Unit>(default);
    }

    private async Task<Result<Unit, IpcError>> ServeAsync(Socket listener, CancellationToken stopping)
    {
        try
        {
            while (true)
            {
                var accepted = await Try.RunAsync(token => listener.AcceptAsync(token).AsTask(), stopping);
                switch (accepted)
                {
                    case Success<Socket> client:
                        _ = ServeConnectionAsync(client.Value, stopping);
                        break;
                    case Failure<Socket> failure:
                        stopping.ThrowIfCancellationRequested();
                        var error = IpcErrors.AsListenFailed(failure.Exception);
                        report(error);
                        return new Fail<IpcError>(error);
                }
            }
        }
        finally
        {
            listener.Dispose();
            foreach (var connection in connections.Keys)
            {
                await connection.Channel.DisposeAsync();
            }

            if (Try.Run(() =>
            {
                File.Delete(socketPath);
                return default(Unit);
            }) is Failure<Unit> deleteFailure)
                report(IpcErrors.AsListenFailed(deleteFailure.Exception));
        }
    }

    private async Task ServeConnectionAsync(Socket socket, CancellationToken stopping)
    {
        var connection = new Connection(new EnvelopeChannel(new NetworkStream(socket, ownsSocket: true)));
        connections[connection] = 0;
        IpcTelemetry.ConnectionOpened(ApplicationConstants.ServerSide);
        IpcLog.ConnectionOpened(log, ApplicationConstants.ServerSide);
        try
        {
            while (true)
            {
                switch (await connection.Channel.ReceiveAsync(stopping))
                {
                    case Ok<Envelope> { Value: RequestEnvelope request }:
                        _ = HandleAsync(connection, request, stopping);
                        break;
                    case Ok<Envelope>:
                        report(new MalformedFrame("A client may only send requests."));
                        return;
                    case Fail<IpcError> failure:
                        if (failure.Error is not ConnectionLost)
                            report(failure.Error);

                        return;
                }
            }
        }
        finally
        {
            connections.TryRemove(connection, out _);
            IpcTelemetry.ConnectionClosed(ApplicationConstants.ServerSide);
            IpcLog.ConnectionClosed(log, ApplicationConstants.ServerSide);
            await connection.Channel.DisposeAsync();
        }
    }

    private async Task HandleAsync(Connection connection, RequestEnvelope request, CancellationToken stopping)
    {
        var outcome = await ObserveAsync(connection, request, stopping);

        Envelope reply = outcome switch
        {
            Ok<object> ok => new ResponseEnvelope(request.Id, ok.Value),
            Fail<IpcError> fail => new ErrorEnvelope(request.Id, fail.Error.Describe(), fail.Error.Info().Code.Value)
        };

        if (await connection.Channel.SendAsync(reply, stopping) is Fail<IpcError> sendFailure && sendFailure.Error is not ConnectionLost)
        {
            report(sendFailure.Error);
            _ = await connection.Channel.SendAsync(new ErrorEnvelope(request.Id, sendFailure.Error.Describe(), sendFailure.Error.Info().Code.Value), stopping);
        }
    }

    private async Task<Result<object, IpcError>> ObserveAsync(Connection connection, RequestEnvelope request, CancellationToken stopping)
    {
        var (kind, type) = request switch
        {
            CommandEnvelope command => (ApplicationConstants.CommandKind, command.Type),
            QueryEnvelope query => (ApplicationConstants.QueryKind, query.Type),
            SubscribeEnvelope => (ApplicationConstants.SubscribeKind, ApplicationConstants.SubscribeMessageType),
            _ => (request.GetType().Name, request.GetType().Name)
        };

        using var activity = IpcTelemetry.StartServerRequest(kind, type, request);
        var started = Stopwatch.GetTimestamp();
        Result<object, IpcError> outcome = request switch
        {
            CommandEnvelope command => await InvokeAsync(commands, command.Type, command.Payload, stopping),
            QueryEnvelope query => await InvokeAsync(queries, query.Type, query.Payload, stopping),
            SubscribeEnvelope => Subscribe(connection),
            _ => new Fail<IpcError>(new MalformedFrame($"Unknown request '{request.GetType().Name}'."))
        };
        IpcTelemetry.CompleteRequest(activity, ApplicationConstants.ServerSide, kind, type, started, outcome);
        if (outcome is Fail<IpcError> failed) IpcLog.RequestFailed(log, ApplicationConstants.ServerSide, kind, type, failed.Error.Describe(), failed.Error.Info().Code);

        return outcome;
    }

    private static async Task<Result<object, IpcError>> InvokeAsync(ConcurrentDictionary<string, Handler> handlers, string type, object payload, CancellationToken stopping)
    {
        if (!handlers.TryGetValue(type, out var handler))
            return new Fail<IpcError>(new HandlerFailed($"No handler is mapped for '{type}'."));

        return await Try.RunAsync(token => handler(payload, token), stopping) switch
        {
            Success<Result<object, IpcError>> ok => ok.Value,
            Failure<Result<object, IpcError>> fail => new Fail<IpcError>(IpcErrors.AsHandlerFailed(fail.Exception))
        };
    }

    private static Result<object, IpcError> Subscribe(Connection connection)
    {
        connection.Subscribed = true;
        return new Ok<object>(default(Unit));
    }

    private async Task<Result<Unit, IpcError>> PushAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (var connection in connections.Keys.Where(connection => connection.Subscribed))
        {
            if (await connection.Channel.SendAsync(envelope, cancellationToken) is Fail<IpcError> failure && failure.Error is not ConnectionLost)
                return failure;
        }

        return new Ok<Unit>(default);
    }
}

using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Serves the IPC contract over a Unix domain socket, one length-prefixed JSON frame per envelope.</summary>
/// <param name="socketPath">Where the socket file is created.</param>
/// <param name="onError">Told about failures nobody else can receive: a broken peer, a dying accept loop.</param>
internal sealed class UnixSocketIpcServer(string socketPath, Action<IpcError>? onError = null) : IIpcServer
{
    private delegate Task<Result<JsonElement, IpcError>> Handler(JsonElement payload, CancellationToken cancellationToken);

    private sealed class Connection(EnvelopeChannel channel)
    {
        public EnvelopeChannel Channel { get; } = channel;

        public volatile bool Subscribed;
    }

    private readonly ConcurrentDictionary<string, Handler> commands = new();
    private readonly ConcurrentDictionary<string, Handler> queries = new();
    private readonly ConcurrentDictionary<Connection, byte> connections = new();
    private readonly Action<IpcError> report = onError ?? (_ => { });
    private CancellationTokenSource? lifetime;
    private Task<Result<Unit, IpcError>> serverLoop = Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    private int disposed;

    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand
        => commands[typeof(TCommand).Name] = (payload, cancellationToken) => RunAsync(payload, handler, cancellationToken);

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult>
        => queries[typeof(TQuery).Name] = (payload, cancellationToken) => RunAsync(payload, handler, cancellationToken);

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

    public async Task<Result<Unit, IpcError>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IpcJson.ToElement(@event) switch
        {
            Ok<JsonElement> payload => await PushAsync(new EventEnvelope(typeof(TEvent).Name, payload.Value), cancellationToken),
            Fail<IpcError> fail => fail
        };
    }

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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        _ = await StopAsync(CancellationToken.None);
        lifetime?.Dispose();
    }

    private static async Task<Result<JsonElement, IpcError>> RunAsync<TRequest, TResponse>(JsonElement payload, Func<TRequest, CancellationToken, Task<Result<TResponse, IpcError>>> handler, CancellationToken cancellationToken)
        => IpcJson.FromElement<TRequest>(payload) switch
        {
            Ok<TRequest> request => await handler(request.Value, cancellationToken) switch
            {
                Ok<TResponse> response => IpcJson.ToElement(response.Value),
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
        File.Delete(socketPath);
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
            await connection.Channel.DisposeAsync();
        }
    }

    private async Task HandleAsync(Connection connection, RequestEnvelope request, CancellationToken stopping)
    {
        Result<JsonElement, IpcError> outcome = request switch
        {
            CommandEnvelope command => await InvokeAsync(commands, command.Type, command.Payload, stopping),
            QueryEnvelope query => await InvokeAsync(queries, query.Type, query.Payload, stopping),
            SubscribeEnvelope => Subscribe(connection),
            _ => new Fail<IpcError>(new MalformedFrame($"Unknown request '{request.GetType().Name}'."))
        };

        Envelope reply = outcome switch
        {
            Ok<JsonElement> ok => new ResponseEnvelope(request.Id, ok.Value),
            Fail<IpcError> fail => new ErrorEnvelope(request.Id, fail.Error.Describe())
        };

        if (await connection.Channel.SendAsync(reply, stopping) is Fail<IpcError> sendFailure && sendFailure.Error is not ConnectionLost)
        {
            report(sendFailure.Error);
            _ = await connection.Channel.SendAsync(new ErrorEnvelope(request.Id, sendFailure.Error.Describe()), stopping);
        }
    }

    private static async Task<Result<JsonElement, IpcError>> InvokeAsync(ConcurrentDictionary<string, Handler> handlers, string type, JsonElement payload, CancellationToken stopping)
    {
        if (!handlers.TryGetValue(type, out var handler))
            return new Fail<IpcError>(new HandlerFailed($"No handler is mapped for '{type}'."));

        return await Try.RunAsync(token => handler(payload, token), stopping) switch
        {
            Success<Result<JsonElement, IpcError>> ok => ok.Value,
            Failure<Result<JsonElement, IpcError>> fail => new Fail<IpcError>(IpcErrors.AsHandlerFailed(fail.Exception))
        };
    }

    private static Result<JsonElement, IpcError> Subscribe(Connection connection)
    {
        connection.Subscribed = true;
        return IpcJson.ToElement(default(Unit));
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

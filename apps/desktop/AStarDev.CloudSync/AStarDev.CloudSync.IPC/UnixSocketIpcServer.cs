using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Serves the IPC contract over a Unix domain socket, one length-prefixed JSON frame per envelope.</summary>
/// <param name="socketPath">Where the socket file is created.</param>
/// <param name="onError">Told about failures nobody else can receive: a broken peer, a dying accept loop.</param>
public sealed class UnixSocketIpcServer(string socketPath, Action<IpcError>? onError = null) : IIpcServer
{
    private delegate Task<Result<JsonElement, IpcError>> Handler(JsonElement payload, CancellationToken cancellationToken);

    private sealed class Connection(EnvelopeChannel channel)
    {
        public EnvelopeChannel Channel { get; } = channel;

        public volatile bool Subscribed;
    }

    private readonly ConcurrentDictionary<string, Handler> _commands = new();
    private readonly ConcurrentDictionary<string, Handler> _queries = new();
    private readonly ConcurrentDictionary<Connection, byte> _connections = new();
    private readonly Action<IpcError> _report = onError ?? (_ => { });
    private CancellationTokenSource? _lifetime;
    private Task<Result<Unit, IpcError>> _serverLoop = Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    private int _disposed;

    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand
        => _commands[typeof(TCommand).Name] = (payload, cancellationToken) => RunAsync(payload, handler, cancellationToken);

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult>
        => _queries[typeof(TQuery).Name] = (payload, cancellationToken) => RunAsync(payload, handler, cancellationToken);

    public Task<Result<Unit, IpcError>> StartAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<Result<Unit, IpcError>>(cancellationToken);
        }

        if (_lifetime is not null)
        {
            return Task.FromResult<Result<Unit, IpcError>>(new Fail<IpcError>(new ListenFailed("The IPC server is already started.")));
        }

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
        if (_lifetime is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new Ok<Unit>(default);
        }

        // the stop is signalled first: a cancelled token only gives up waiting for it to finish
        await _lifetime.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
        // WhenAny: the loop ends canceled on a normal stop, and awaiting it directly would throw
        await Task.WhenAny(_serverLoop).WaitAsync(cancellationToken);

        return _serverLoop.IsCompletedSuccessfully ? _serverLoop.Result : new Ok<Unit>(default);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // a failure that ended the accept loop was already reported to onError when it happened
        _ = await StopAsync(CancellationToken.None);
        _lifetime?.Dispose();
    }

    private static async Task<Result<JsonElement, IpcError>> RunAsync<TRequest, TResponse>(
        JsonElement payload,
        Func<TRequest, CancellationToken, Task<Result<TResponse, IpcError>>> handler,
        CancellationToken cancellationToken)
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
            // shortcut: a leftover socket file is assumed stale and replaced; the host must enforce a single daemon instance.
            File.Delete(socketPath);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            // local machine, single user: only the owner may connect
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            listener.Listen();
            return listener;
        });

        if (opened is Fail<Exception>)
        {
            listener.Dispose();
        }

        return opened.MapError(IpcErrors.AsListenFailed);
    }

    private Result<Unit, IpcError> Launch(Socket listener, CancellationToken cancellationToken)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serverLoop = ServeAsync(listener, _lifetime.Token);
        return new Ok<Unit>(default);
    }

    /// <summary>Accepts until stopped, then closes everything it opened.</summary>
    private async Task<Result<Unit, IpcError>> ServeAsync(Socket listener, CancellationToken stopping)
    {
        try
        {
            while (true)
            {
                var accepted = await Try.RunAsync(token => listener.AcceptAsync(token).AsTask(), stopping);
                switch (accepted)
                {
                    case Ok<Socket> client:
                        _ = ServeConnectionAsync(client.Value, stopping);
                        break;
                    case Fail<Exception> failure:
                        // a failure caused by stopping is a stop, not a failure
                        stopping.ThrowIfCancellationRequested();
                        var error = IpcErrors.AsListenFailed(failure.Error);
                        _report(error);
                        return new Fail<IpcError>(error);
                }
            }
        }
        finally
        {
            listener.Dispose();
            foreach (var connection in _connections.Keys)
            {
                await connection.Channel.DisposeAsync();
            }

            if (Try.Run(() =>
            {
                File.Delete(socketPath);
                return default(Unit);
            }) is Fail<Exception> deleteFailure)
            {
                _report(IpcErrors.AsListenFailed(deleteFailure.Error));
            }
        }
    }

    private async Task ServeConnectionAsync(Socket socket, CancellationToken stopping)
    {
        var connection = new Connection(new EnvelopeChannel(new NetworkStream(socket, ownsSocket: true)));
        _connections[connection] = 0;
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
                        _report(new MalformedFrame("A client may only send requests."));
                        return;
                    case Fail<IpcError> failure:
                        // a peer that hangs up is normal; a broken or hostile one costs only its own connection
                        if (failure.Error is not ConnectionLost)
                        {
                            _report(failure.Error);
                        }

                        return;
                }
            }
        }
        finally
        {
            _connections.TryRemove(connection, out _);
            await connection.Channel.DisposeAsync();
        }
    }

    private async Task HandleAsync(Connection connection, RequestEnvelope request, CancellationToken stopping)
    {
        Result<JsonElement, IpcError> outcome = request switch
        {
            CommandEnvelope command => await InvokeAsync(_commands, command.Type, command.Payload, stopping),
            QueryEnvelope query => await InvokeAsync(_queries, query.Type, query.Payload, stopping),
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
            // e.g. the answer is too large: report it and tell the caller, who would otherwise wait forever
            _report(sendFailure.Error);
            _ = await connection.Channel.SendAsync(new ErrorEnvelope(request.Id, sendFailure.Error.Describe()), stopping);
        }
    }

    private static async Task<Result<JsonElement, IpcError>> InvokeAsync(
        ConcurrentDictionary<string, Handler> handlers,
        string type,
        JsonElement payload,
        CancellationToken stopping)
    {
        if (!handlers.TryGetValue(type, out var handler))
        {
            return new Fail<IpcError>(new HandlerFailed($"No handler is mapped for '{type}'."));
        }

        // a handler is someone else's code: if it throws, the caller gets a failure and the daemon keeps running
        return await Try.RunAsync(token => handler(payload, token), stopping) switch
        {
            Ok<Result<JsonElement, IpcError>> ok => ok.Value,
            Fail<Exception> fail => new Fail<IpcError>(IpcErrors.AsHandlerFailed(fail.Error))
        };
    }

    private static Result<JsonElement, IpcError> Subscribe(Connection connection)
    {
        connection.Subscribed = true;
        return IpcJson.ToElement(default(Unit));
    }

    private async Task<Result<Unit, IpcError>> PushAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (var connection in _connections.Keys.Where(connection => connection.Subscribed))
        {
            // a subscriber that is gone is skipped; any other failure is about the event itself, so it would hit everyone
            if (await connection.Channel.SendAsync(envelope, cancellationToken) is Fail<IpcError> failure && failure.Error is not ConnectionLost)
            {
                return failure;
            }
        }

        return new Ok<Unit>(default);
    }
}

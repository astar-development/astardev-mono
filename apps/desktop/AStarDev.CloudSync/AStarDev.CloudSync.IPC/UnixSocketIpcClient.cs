using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Sockets;
using System.Text.Json;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Talks to a <see cref="UnixSocketIpcServer"/>. One instance is one connection; create a new one to reconnect.</summary>
/// <param name="socketPath">The server's socket file.</param>
/// <param name="onError">Told about failures nobody else can receive: a failing event handler, a broken frame.</param>
public sealed class UnixSocketIpcClient(string socketPath, Action<IpcError>? onError = null) : IIpcClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Result<JsonElement, IpcError>>> _pending = new();
    private readonly ConcurrentDictionary<string, ImmutableList<Func<JsonElement, Result<Unit, IpcError>>>> _eventHandlers = new();
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _closing = new();
    private readonly Action<IpcError> _report = onError ?? (_ => { });
    private EnvelopeChannel? _channel;
    private Task _readLoop = Task.CompletedTask;
    private long _lastId;
    private int _disposed;

    public Task Disconnected => _disconnected.Task;

    public async Task<Result<Unit, IpcError>> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_channel is not null || _disposed == 1)
        {
            return new Fail<IpcError>(new ConnectionFailed("The IPC client is already connected or was disposed."));
        }

        return await OpenAsync(cancellationToken) switch
        {
            Ok<Socket> socket => Attach(socket.Value),
            Fail<IpcError> fail => fail
        };
    }

    public Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken)
        => RequestAsync<CommandResult>(command, (id, payload) => new CommandEnvelope(id, command.GetType().Name, payload), cancellationToken);

    public Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        => RequestAsync<TResult>(query, (id, payload) => new QueryEnvelope(id, query.GetType().Name, payload), cancellationToken);

    public Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken)
        => RequestAsync<Unit>(default(Unit), (id, _) => new SubscribeEnvelope(id), cancellationToken);

    public void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        Func<JsonElement, Result<Unit, IpcError>> handle = payload => IpcJson.FromElement<TEvent>(payload) switch
        {
            Ok<TEvent> @event => Try.Run(() =>
            {
                handler(@event.Value);
                return default(Unit);
            }).MapError(IpcErrors.AsHandlerFailed),
            Fail<IpcError> fail => fail
        };

        _eventHandlers.AddOrUpdate(typeof(TEvent).Name, _ => ImmutableList.Create(handle), (_, existing) => existing.Add(handle));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _closing.CancelAsync();
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        // WhenAny: the read loop ends canceled on dispose, and awaiting it directly would throw
        await Task.WhenAny(_readLoop);
        _disconnected.TrySetResult();
        _closing.Dispose();
    }

    private async Task<Result<Socket, IpcError>> OpenAsync(CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var connected = false;
        try
        {
            var opened = await Try.RunAsync(async token =>
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), token);
                return socket;
            }, cancellationToken);
            connected = opened is Ok<Socket>;

            return opened.MapError(IpcErrors.AsConnectionFailed);
        }
        finally
        {
            // a failed or cancelled attempt must not leave a socket behind
            if (!connected)
            {
                socket.Dispose();
            }
        }
    }

    private Result<Unit, IpcError> Attach(Socket socket)
    {
        _channel = new EnvelopeChannel(new NetworkStream(socket, ownsSocket: true));
        _readLoop = ReadLoopAsync(_channel, _closing.Token);
        return new Ok<Unit>(default);
    }

    private async Task<Result<TResponse, IpcError>> RequestAsync<TResponse>(
        object message,
        Func<long, JsonElement, RequestEnvelope> create,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IpcJson.ToElement(message) switch
        {
            Ok<JsonElement> payload => await ExchangeAsync(id => create(id, payload.Value), cancellationToken) switch
            {
                Ok<JsonElement> response => IpcJson.FromElement<TResponse>(response.Value),
                Fail<IpcError> exchangeFailure => exchangeFailure
            },
            Fail<IpcError> encodeFailure => encodeFailure
        };
    }

    /// <summary>Sends one request and waits for the answer with the same id.</summary>
    private async Task<Result<JsonElement, IpcError>> ExchangeAsync(Func<long, RequestEnvelope> create, CancellationToken cancellationToken)
    {
        if (_channel is not { } channel)
        {
            return new Fail<IpcError>(new NotConnected());
        }

        var id = Interlocked.Increment(ref _lastId);
        var waiting = new TaskCompletionSource<Result<JsonElement, IpcError>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiting;
        try
        {
            // checked after registering, so a request can never wait on a connection that already ended
            if (_disconnected.Task.IsCompleted)
            {
                return new Fail<IpcError>(new ConnectionLost("The connection has ended."));
            }

            return await channel.SendAsync(create(id), cancellationToken) switch
            {
                Ok<Unit> => await waiting.Task.WaitAsync(cancellationToken),
                Fail<IpcError> sendFailure => sendFailure
            };
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task ReadLoopAsync(EnvelopeChannel channel, CancellationToken closing)
    {
        try
        {
            while (true)
            {
                switch (await channel.ReceiveAsync(closing))
                {
                    case Ok<Envelope> { Value: var envelope }:
                        Dispatch(envelope);
                        break;
                    case Fail<IpcError> failure:
                        // the server hanging up is the normal way a connection ends; anything else is worth knowing
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
            // completed before the pending requests are failed: see ExchangeAsync
            _disconnected.TrySetResult();
            foreach (var id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out var waiting))
                {
                    waiting.TrySetResult(new Fail<IpcError>(new ConnectionLost("The connection ended before the server answered.")));
                }
            }
        }
    }

    private void Dispatch(Envelope envelope)
    {
        switch (envelope)
        {
            case ResponseEnvelope response when _pending.TryRemove(response.Id, out var answered):
                answered.TrySetResult(new Ok<JsonElement>(response.Payload));
                break;
            case ErrorEnvelope error when _pending.TryRemove(error.Id, out var refused):
                refused.TrySetResult(new Fail<IpcError>(new HandlerFailed(error.Message)));
                break;
            case EventEnvelope @event when _eventHandlers.TryGetValue(@event.Type, out var handlers):
                foreach (var handle in handlers)
                {
                    // a failing handler is reported, never fatal: the other handlers and the connection carry on
                    if (handle(@event.Payload) is Fail<IpcError> failure)
                    {
                        _report(failure.Error);
                    }
                }

                break;
        }
    }
}

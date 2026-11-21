using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Sockets;
using System.Text.Json;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Talks to a <see cref="UnixSocketIpcServer"/>. One instance is one connection; create a new one to reconnect.</summary>
/// <param name="socketPath">The server's socket file.</param>
/// <param name="onError">Told about failures nobody else can receive: a failing event handler, a broken frame.</param>
internal sealed class UnixSocketIpcClient(string socketPath, Action<IpcError>? onError = null) : IIpcClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Result<JsonElement, IpcError>>> pending = new();
    private readonly ConcurrentDictionary<string, ImmutableList<Func<JsonElement, Result<Unit, IpcError>>>> eventHandlers = new();
    private readonly TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource closing = new();
    private readonly Action<IpcError> report = onError ?? (_ => { });
    private EnvelopeChannel? channel;
    private Task readLoop = Task.CompletedTask;
    private long lastId;
    private int disposed;

    public Task Disconnected => disconnected.Task;

    public async Task<Result<Unit, IpcError>> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (channel is not null || disposed == 1)
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
            }).ToResult(IpcErrors.AsHandlerFailed),
            Fail<IpcError> fail => fail
        };

        eventHandlers.AddOrUpdate(typeof(TEvent).Name, _ => ImmutableList.Create(handle), (_, existing) => existing.Add(handle));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
            return;

        await closing.CancelAsync();
        if (channel is not null)
            await channel.DisposeAsync();

        await Task.WhenAny(readLoop);
        disconnected.TrySetResult();
        closing.Dispose();
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
            connected = opened is Success<Socket>;

            return opened.ToResult(IpcErrors.AsConnectionFailed);
        }
        finally
        {
            if (!connected)
            {
                socket.Dispose();
            }
        }
    }

    private Result<Unit, IpcError> Attach(Socket socket)
    {
        channel = new EnvelopeChannel(new NetworkStream(socket, ownsSocket: true));
        readLoop = ReadLoopAsync(channel, closing.Token);

        return new Ok<Unit>(default);
    }

    private async Task<Result<TResponse, IpcError>> RequestAsync<TResponse>(object message, Func<long, JsonElement, RequestEnvelope> create, CancellationToken cancellationToken)
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

    private async Task<Result<JsonElement, IpcError>> ExchangeAsync(Func<long, RequestEnvelope> create, CancellationToken cancellationToken)
    {
        if (channel is not { } openChannel)
            return new Fail<IpcError>(new NotConnected());

        var id = Interlocked.Increment(ref lastId);
        var waiting = new TaskCompletionSource<Result<JsonElement, IpcError>>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = waiting;
        try
        {
            if (disconnected.Task.IsCompleted)
                return new Fail<IpcError>(new ConnectionLost("The connection has ended."));

            return await openChannel.SendAsync(create(id), cancellationToken) switch
            {
                Ok<Unit> => await waiting.Task.WaitAsync(cancellationToken),
                Fail<IpcError> sendFailure => sendFailure
            };
        }
        finally
        {
            pending.TryRemove(id, out _);
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
                        if (failure.Error is not ConnectionLost)
                            report(failure.Error);

                        return;
                }
            }
        }
        finally
        {
            disconnected.TrySetResult();
            foreach (var id in pending.Keys)
            {
                if (pending.TryRemove(id, out var waiting))
                    waiting.TrySetResult(new Fail<IpcError>(new ConnectionLost("The connection ended before the server answered.")));
            }
        }
    }

    private void Dispatch(Envelope envelope)
    {
        switch (envelope)
        {
            case ResponseEnvelope response when pending.TryRemove(response.Id, out var answered):
                answered.TrySetResult(new Ok<JsonElement>(response.Payload));
                break;
            case ErrorEnvelope error when pending.TryRemove(error.Id, out var refused):
                refused.TrySetResult(new Fail<IpcError>(new HandlerFailed(error.Message)));
                break;
            case EventEnvelope @event when eventHandlers.TryGetValue(@event.Type, out var handlers):
                foreach (var handle in handlers)
                {
                    if (handle(@event.Payload) is Fail<IpcError> failure)
                        report(failure.Error);
                }

                break;
        }
    }
}

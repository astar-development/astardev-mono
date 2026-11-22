using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.IPC;

/// <summary>Talks to a <see cref="UnixSocketIpcServer"/>. One instance is one connection; create a new one to reconnect.</summary>
/// <param name="socketPath">The server's socket file.</param>
/// <param name="onError">Told about failures nobody else can receive: a failing event handler, a broken frame.</param>
/// <param name="logger">Receives connection and failed-request logs; omit to log nothing.</param>
internal sealed class UnixSocketIpcClient(string socketPath, Action<IpcError>? onError = null, ILogger? logger = null) : IIpcClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Result<JsonElement, IpcError>>> pending = new();
    private readonly ConcurrentDictionary<string, ImmutableList<Func<JsonElement, Result<Unit, IpcError>>>> eventHandlers = new();
    private readonly TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource closing = new();
    private readonly Action<IpcError> report = onError ?? (_ => { });
    private readonly ILogger log = logger ?? NullLogger.Instance;
    private EnvelopeChannel? channel;
    private Task readLoop = Task.CompletedTask;
    private long lastId;
    private int disposed;

    /// <inheritdoc/>
    public Task Disconnected => disconnected.Task;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public Task<Result<CommandResult, IpcError>> SendAsync(ICommand command, CancellationToken cancellationToken)
        => RequestAsync<CommandResult>(ApplicationConstants.CommandKind, command.GetType().Name, command, (id, payload) => new CommandEnvelope(id, command.GetType().Name, payload), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<TResult, IpcError>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
        => RequestAsync<TResult>(ApplicationConstants.QueryKind, query.GetType().Name, query, (id, payload) => new QueryEnvelope(id, query.GetType().Name, payload), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<Unit, IpcError>> SubscribeAsync(CancellationToken cancellationToken)
        => RequestAsync<Unit>(ApplicationConstants.SubscribeKind, ApplicationConstants.SubscribeMessageType, default(Unit), (id, _) => new SubscribeEnvelope(id), cancellationToken);

    /// <inheritdoc/>
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

    /// <summary>Closes the connection: requests still waiting fail with <see cref="ConnectionLost"/> and <see cref="Disconnected"/> completes. Safe to call more than once.</summary>
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
        IpcTelemetry.ConnectionOpened(ApplicationConstants.ClientSide);
        IpcLog.ConnectionOpened(log, ApplicationConstants.ClientSide);
        readLoop = ReadLoopAsync(channel, closing.Token);

        return new Ok<Unit>(default);
    }

    private async Task<Result<TResponse, IpcError>> RequestAsync<TResponse>(string kind, string type, object message, Func<long, JsonElement, RequestEnvelope> create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = IpcTelemetry.StartClientRequest(kind, type);
        var started = Stopwatch.GetTimestamp();
        Result<TResponse, IpcError> result = IpcJson.ToElement(message) switch
        {
            Ok<JsonElement> payload => await ExchangeAsync(id => IpcTelemetry.Inject(create(id, payload.Value), activity), cancellationToken) switch
            {
                Ok<JsonElement> response => IpcJson.FromElement<TResponse>(response.Value),
                Fail<IpcError> exchangeFailure => exchangeFailure
            },
            Fail<IpcError> encodeFailure => encodeFailure
        };
        IpcTelemetry.CompleteRequest(activity, ApplicationConstants.ClientSide, kind, type, started, result);
        if (result is Fail<IpcError> failed) IpcLog.RequestFailed(log, ApplicationConstants.ClientSide, kind, type, failed.Error.Describe());

        return result;
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
            IpcTelemetry.ConnectionClosed(ApplicationConstants.ClientSide);
            IpcLog.ConnectionClosed(log, ApplicationConstants.ClientSide);
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
                Handle(@event, handlers);
                break;
        }
    }

    private void Handle(EventEnvelope @event, ImmutableList<Func<JsonElement, Result<Unit, IpcError>>> handlers)
    {
        using var activity = IpcTelemetry.StartEventReceive(@event);
        foreach (var handle in handlers)
        {
            if (handle(@event.Payload) is Fail<IpcError> failure)
                report(failure.Error);
        }
    }
}

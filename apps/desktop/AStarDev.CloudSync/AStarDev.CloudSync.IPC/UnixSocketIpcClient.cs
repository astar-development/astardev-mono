using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;

namespace AStarDev.CloudSync.IPC;

/// <summary>Talks to a <see cref="UnixSocketIpcServer"/>. One instance is one connection; create a new one to reconnect.</summary>
public sealed class UnixSocketIpcClient(string socketPath) : IIpcClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<ResponseEnvelope>> _pending = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _eventHandlers = new();
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _closing = new();
    private EnvelopeChannel? _channel;
    private Task _readLoop = Task.CompletedTask;
    private long _lastId;
    private int _disposed;

    public Task Disconnected => _disconnected.Task;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null)
        {
            throw new InvalidOperationException("The IPC client is already connected.");
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        _channel = new EnvelopeChannel(new NetworkStream(socket, ownsSocket: true));
        _readLoop = ReadLoopAsync(_channel);
    }

    public async Task<CommandResult> SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync(id => new CommandEnvelope(id, command.GetType().Name, IpcJson.ToElement(command)), cancellationToken);
        return IpcJson.FromElement<CommandResult>(response.Payload ?? throw new IpcException("The server sent no result."));
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var response = await RequestAsync(id => new QueryEnvelope(id, query.GetType().Name, IpcJson.ToElement(query)), cancellationToken);
        return IpcJson.FromElement<TResult>(response.Payload ?? throw new IpcException("The server sent no result."));
    }

    public void OnEvent<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        Action<JsonElement> handle = payload => handler(IpcJson.FromElement<TEvent>(payload));
        _eventHandlers.AddOrUpdate(typeof(TEvent).Name, handle, (_, existing) => existing + handle);
    }

    public async Task SubscribeAsync(CancellationToken cancellationToken = default)
        => await RequestAsync(id => new SubscribeEnvelope(id), cancellationToken);

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

        await _readLoop;
        _disconnected.TrySetResult();
        _closing.Dispose();
    }

    private async Task<ResponseEnvelope> RequestAsync(Func<long, RequestEnvelope> create, CancellationToken cancellationToken)
    {
        var channel = _channel ?? throw new IOException("The IPC client is not connected.");
        var id = Interlocked.Increment(ref _lastId);
        var waiting = new TaskCompletionSource<ResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiting;
        try
        {
            // checked after registering, so a request can never wait on a connection that already ended
            if (_disconnected.Task.IsCompleted)
            {
                throw new IOException("The IPC connection was lost.");
            }

            await channel.SendAsync(create(id), cancellationToken);
            var response = await waiting.Task.WaitAsync(cancellationToken);
            return response.Error is null ? response : throw new IpcException(response.Error);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task ReadLoopAsync(EnvelopeChannel channel)
    {
        try
        {
            while (await channel.ReceiveAsync(_closing.Token) is { } envelope)
            {
                switch (envelope)
                {
                    case ResponseEnvelope response when _pending.TryRemove(response.Id, out var waiting):
                        waiting.TrySetResult(response);
                        break;
                    case EventEnvelope @event when _eventHandlers.TryGetValue(@event.Type, out var handler):
                        try
                        {
                            handler(@event.Payload);
                        }
                        catch (Exception)
                        {
                            // shortcut: a failing handler must not drop the connection; log it once Observability lands (S5, S6).
                        }

                        break;
                }
            }
        }
        catch (Exception)
        {
            // a lost or broken connection ends the loop; callers learn of it through Disconnected
        }
        finally
        {
            _disconnected.TrySetResult();
            foreach (var id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out var waiting))
                {
                    waiting.TrySetException(new IOException("The IPC connection was lost."));
                }
            }
        }
    }
}

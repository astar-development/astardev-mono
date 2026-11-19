using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;

namespace AStarDev.CloudSync.IPC;

/// <summary>Serves the IPC contract over a Unix domain socket, one length-prefixed JSON frame per envelope.</summary>
public sealed class UnixSocketIpcServer(string socketPath) : IIpcServer
{
    private delegate Task<JsonElement> Handler(JsonElement payload, CancellationToken cancellationToken);

    private sealed class Connection(EnvelopeChannel channel)
    {
        public EnvelopeChannel Channel { get; } = channel;

        public volatile bool Subscribed;
    }

    private readonly ConcurrentDictionary<string, Handler> _commands = new();
    private readonly ConcurrentDictionary<string, Handler> _queries = new();
    private readonly ConcurrentDictionary<Connection, byte> _connections = new();
    private readonly CancellationTokenSource _stopping = new();
    private Socket? _listener;
    private Task _acceptLoop = Task.CompletedTask;
    private int _disposed;

    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<CommandResult>> handler)
        where TCommand : ICommand
        => _commands[typeof(TCommand).Name] = async (payload, cancellationToken) =>
            IpcJson.ToElement(await handler(IpcJson.FromElement<TCommand>(payload), cancellationToken));

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<TResult>> handler)
        where TQuery : IQuery<TResult>
        => _queries[typeof(TQuery).Name] = async (payload, cancellationToken) =>
            IpcJson.ToElement(await handler(IpcJson.FromElement<TQuery>(payload), cancellationToken) ?? throw new InvalidOperationException("A query handler returned null."));

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        // shortcut: a leftover socket file is assumed stale and replaced; the host must enforce a single daemon instance.
        File.Delete(socketPath);
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        // local machine, single user: only the owner may connect
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        _listener.Listen();
        _acceptLoop = AcceptLoopAsync(_listener);
        return Task.CompletedTask;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        var envelope = new EventEnvelope(typeof(TEvent).Name, IpcJson.ToElement(@event));
        foreach (var connection in _connections.Keys.Where(connection => connection.Subscribed))
        {
            try
            {
                await connection.Channel.SendAsync(envelope, cancellationToken);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // that subscriber is gone; its connection loop removes it, the others still get the event
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync();
        _listener?.Dispose();
        await _acceptLoop;
        foreach (var connection in _connections.Keys)
        {
            await connection.Channel.DisposeAsync();
        }

        if (_listener is not null)
        {
            File.Delete(socketPath);
        }

        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync(Socket listener)
    {
        try
        {
            while (true)
            {
                var socket = await listener.AcceptAsync(_stopping.Token);
                _ = RunConnectionAsync(socket);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // shortcut: an accept failure ends the loop silently; add logging and a restart once Observability lands (S5, S6).
        }
    }

    private async Task RunConnectionAsync(Socket socket)
    {
        var connection = new Connection(new EnvelopeChannel(new NetworkStream(socket, ownsSocket: true)));
        _connections[connection] = 0;
        try
        {
            while (await connection.Channel.ReceiveAsync(_stopping.Token) is { } envelope)
            {
                if (envelope is RequestEnvelope request)
                {
                    _ = HandleAsync(connection, request);
                }
            }
        }
        catch (Exception)
        {
            // a broken or misbehaving peer only costs its own connection, never the daemon
        }
        finally
        {
            _connections.TryRemove(connection, out _);
            await connection.Channel.DisposeAsync();
        }
    }

    private async Task HandleAsync(Connection connection, RequestEnvelope request)
    {
        ResponseEnvelope response;
        try
        {
            JsonElement? payload = null;
            switch (request)
            {
                case CommandEnvelope command:
                    payload = await Invoke(_commands, command.Type, command.Payload);
                    break;
                case QueryEnvelope query:
                    payload = await Invoke(_queries, query.Type, query.Payload);
                    break;
                case SubscribeEnvelope:
                    connection.Subscribed = true;
                    break;
            }

            response = new ResponseEnvelope(request.Id, payload, null);
        }
        catch (Exception e)
        {
            response = new ResponseEnvelope(request.Id, null, e.Message);
        }

        try
        {
            await connection.Channel.SendAsync(response, _stopping.Token);
        }
        catch (Exception)
        {
            // the caller went away before the answer; its connection loop cleans up
        }
    }

    private Task<JsonElement> Invoke(ConcurrentDictionary<string, Handler> handlers, string type, JsonElement payload)
        => handlers.TryGetValue(type, out var handler)
            ? handler(payload, _stopping.Token)
            : throw new InvalidOperationException($"No handler is mapped for '{type}'.");
}

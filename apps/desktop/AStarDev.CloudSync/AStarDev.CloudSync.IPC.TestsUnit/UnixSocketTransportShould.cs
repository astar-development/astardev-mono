using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC.TestsUnit;

// A new instance is created per test, so every test gets its own socket path.
public sealed class UnixSocketTransportShould : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly DaemonStatusView Status = new(DaemonState.Running, "1.2.3", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private sealed record PingCommand(string Text) : ICommand;

    private sealed record ProbeQuery(string Mode) : IQuery<DaemonStatusView>;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string _socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-ipc-{Guid.NewGuid():N}.sock");
    private readonly TaskCompletionSource<IpcError> _firstServerError = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<IpcError> _clientErrors = new();
    private readonly UnixSocketIpcServer _server;
    private readonly UnixSocketIpcClient _client;

    public UnixSocketTransportShould()
    {
        _server = new UnixSocketIpcServer(_socketPath, error => _firstServerError.TrySetResult(error));
        _client = new UnixSocketIpcClient(_socketPath, _clientErrors.Enqueue);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        File.Delete(_socketPath);
    }

    private static Task<Result<T, IpcError>> Succeed<T>(T value) => Task.FromResult<Result<T, IpcError>>(new Ok<T>(value));

    private static Task<Result<T, IpcError>> FailWith<T>(IpcError error) => Task.FromResult<Result<T, IpcError>>(new Fail<IpcError>(error));

    private async Task StartAndConnectAsync()
    {
        (await _server.StartAsync(Token)).ShouldSucceed();
        (await _client.ConnectAsync(Token)).ShouldSucceed();
    }

    private static async Task<CancellationToken> CancelledTokenAsync()
    {
        var source = new CancellationTokenSource();
        await source.CancelAsync();
        return source.Token;
    }

    [Fact]
    public async Task serve_a_query_over_a_real_socket()
    {
        _server.MapQuery((GetDaemonStatus _, CancellationToken _) => Succeed(Status));
        await StartAndConnectAsync();

        var result = await _client.QueryAsync(new GetDaemonStatus(), Token).WaitAsync(Timeout, Token);

        result.ShouldSucceed().ShouldBe(Status);
    }

    [Fact]
    public async Task push_events_to_a_subscriber()
    {
        var received = new TaskCompletionSource<DaemonStatusChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.OnEvent<DaemonStatusChanged>(received.SetResult);
        await StartAndConnectAsync();
        (await _client.SubscribeAsync(Token).WaitAsync(Timeout, Token)).ShouldSucceed();

        (await _server.PublishAsync(new DaemonStatusChanged(Status), Token)).ShouldSucceed();

        (await received.Task.WaitAsync(Timeout, Token)).ShouldBe(new DaemonStatusChanged(Status));
    }

    [Fact]
    public async Task tell_the_client_when_the_server_goes_away()
    {
        await StartAndConnectAsync();
        (await _client.SubscribeAsync(Token).WaitAsync(Timeout, Token)).ShouldSucceed();
        _client.Disconnected.IsCompleted.ShouldBeFalse();

        (await _server.StopAsync(Token)).ShouldSucceed();

        await _client.Disconnected.WaitAsync(Timeout, Token);
        (await _client.QueryAsync(new GetDaemonStatus(), Token)).ShouldFail().Value.ShouldBeOfType<ConnectionLost>();
    }

    [Fact]
    public async Task send_a_command_and_return_its_outcome()
    {
        _server.MapCommand((PingCommand command, CancellationToken _) =>
            Succeed(new CommandResult(CommandOutcome.Accepted, command.Text)));
        await StartAndConnectAsync();

        var result = await _client.SendAsync(new PingCommand("hello"), Token).WaitAsync(Timeout, Token);

        result.ShouldSucceed().ShouldBe(new CommandResult(CommandOutcome.Accepted, "hello"));
    }

    [Fact]
    public async Task report_a_handler_failure_to_the_caller()
    {
        _server.MapQuery((ProbeQuery query, CancellationToken _) => query.Mode switch
        {
            "fail" => FailWith<DaemonStatusView>(new HandlerFailed("denied")),
            "throw" => throw new InvalidOperationException("boom"),
            _ => Succeed(Status)
        });
        await StartAndConnectAsync();

        var failed = await _client.QueryAsync(new ProbeQuery("fail"), Token).WaitAsync(Timeout, Token);
        var threw = await _client.QueryAsync(new ProbeQuery("throw"), Token).WaitAsync(Timeout, Token);
        var unmapped = await _client.SendAsync(new PingCommand("unmapped"), Token).WaitAsync(Timeout, Token);

        failed.ShouldFail().Value.ShouldBeOfType<HandlerFailed>().Message.ShouldBe("denied");
        threw.ShouldFail().Value.ShouldBeOfType<HandlerFailed>().Message.ShouldContain("boom");
        unmapped.ShouldFail().Value.ShouldBeOfType<HandlerFailed>().Message.ShouldContain(nameof(PingCommand));

        // a failing handler costs only its own request, the connection stays usable
        (await _client.QueryAsync(new ProbeQuery("ok"), Token).WaitAsync(Timeout, Token)).ShouldSucceed().ShouldBe(Status);
    }

    [Fact]
    public async Task answer_with_a_failure_when_the_response_is_too_large()
    {
        _server.MapQuery((GetDaemonStatus _, CancellationToken _) =>
            Succeed(Status with { Version = new string('x', FrameCodec.MaxFrameSize) }));
        await StartAndConnectAsync();

        var result = await _client.QueryAsync(new GetDaemonStatus(), Token).WaitAsync(Timeout, Token);

        result.ShouldFail().Value.ShouldBeOfType<HandlerFailed>().Message.ShouldContain("exceeds the maximum");
        (await _firstServerError.Task.WaitAsync(Timeout, Token)).Value.ShouldBeOfType<FrameTooLarge>();
    }

    [Fact]
    public async Task fail_a_request_made_before_connecting()
    {
        (await _client.QueryAsync(new GetDaemonStatus(), Token)).ShouldFail().Value.ShouldBeOfType<NotConnected>();
        (await _client.SendAsync(new PingCommand("early"), Token)).ShouldFail().Value.ShouldBeOfType<NotConnected>();
        (await _client.SubscribeAsync(Token)).ShouldFail().Value.ShouldBeOfType<NotConnected>();
    }

    [Fact]
    public async Task fail_to_connect_when_no_server_is_listening()
    {
        var result = await _client.ConnectAsync(Token);

        result.ShouldFail().Value.ShouldBeOfType<ConnectionFailed>();
    }

    [Fact]
    public async Task fail_to_start_when_the_socket_path_is_unusable()
    {
        await using var server = new UnixSocketIpcServer(Path.Combine(_socketPath, "missing-directory", "ipc.sock"));

        var result = await server.StartAsync(Token);

        result.ShouldFail().Value.ShouldBeOfType<ListenFailed>();
    }

    [Fact]
    public async Task honour_cancellation_of_a_pending_request()
    {
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.MapQuery(async (GetDaemonStatus _, CancellationToken cancellationToken) =>
        {
            handlerStarted.SetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            return await Succeed(Status);
        });
        await StartAndConnectAsync();
        using var cancelling = new CancellationTokenSource();

        var pending = _client.QueryAsync(new GetDaemonStatus(), cancelling.Token);
        await handlerStarted.Task.WaitAsync(Timeout, Token);
        await cancelling.CancelAsync();

        await ResultAssertions.ShouldBeCancelledAsync(async () => await pending.WaitAsync(Timeout, Token));
        _client.Disconnected.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task honour_cancellation_while_connecting()
    {
        (await _server.StartAsync(Token)).ShouldSucceed();
        var cancelled = await CancelledTokenAsync();

        await ResultAssertions.ShouldBeCancelledAsync(async () => await _client.ConnectAsync(cancelled));

        // the cancelled attempt left no half-open connection behind
        (await _client.QueryAsync(new GetDaemonStatus(), Token)).ShouldFail().Value.ShouldBeOfType<NotConnected>();
        (await _client.ConnectAsync(Token)).ShouldSucceed();
    }

    [Fact]
    public async Task honour_an_already_cancelled_token_on_every_operation()
    {
        await StartAndConnectAsync();
        var cancelled = await CancelledTokenAsync();
        await using var fresh = new UnixSocketIpcServer(Path.Combine(Path.GetTempPath(), $"cloudsync-ipc-{Guid.NewGuid():N}.sock"));

        await ResultAssertions.ShouldBeCancelledAsync(async () => await _client.QueryAsync(new GetDaemonStatus(), cancelled));
        await ResultAssertions.ShouldBeCancelledAsync(async () => await _client.SendAsync(new PingCommand("late"), cancelled));
        await ResultAssertions.ShouldBeCancelledAsync(async () => await _client.SubscribeAsync(cancelled));
        await ResultAssertions.ShouldBeCancelledAsync(async () => await _server.PublishAsync(new DaemonStatusChanged(Status), cancelled));
        await ResultAssertions.ShouldBeCancelledAsync(async () => await fresh.StartAsync(cancelled));

        // a refused start left nothing behind, and the connected pair is still healthy
        _client.Disconnected.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task stop_the_accept_loop_when_the_server_is_cancelled()
    {
        using var lifetime = new CancellationTokenSource();
        (await _server.StartAsync(lifetime.Token)).ShouldSucceed();
        (await _client.ConnectAsync(Token)).ShouldSucceed();

        await lifetime.CancelAsync();
        (await _server.StopAsync(Token)).ShouldSucceed();

        await _client.Disconnected.WaitAsync(Timeout, Token);
        File.Exists(_socketPath).ShouldBeFalse();
        await using var late = new UnixSocketIpcClient(_socketPath);
        (await late.ConnectAsync(Token)).ShouldFail().Value.ShouldBeOfType<ConnectionFailed>();
    }

    [Fact]
    public async Task stop_waiting_for_the_shutdown_when_the_stop_is_cancelled()
    {
        await StartAndConnectAsync();
        var cancelled = await CancelledTokenAsync();

        await ResultAssertions.ShouldBeCancelledAsync(async () => await _server.StopAsync(cancelled));

        // the stop was only abandoned, not undone: calling it again completes the shutdown
        (await _server.StopAsync(Token)).ShouldSucceed();
        await _client.Disconnected.WaitAsync(Timeout, Token);
    }

    [Fact]
    public async Task report_a_failing_event_handler_and_keep_delivering_events()
    {
        var delivered = new TaskCompletionSource<DaemonStatusChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.OnEvent<DaemonStatusChanged>(_ => throw new InvalidOperationException("handler bug"));
        _client.OnEvent<DaemonStatusChanged>(delivered.SetResult);
        await StartAndConnectAsync();
        (await _client.SubscribeAsync(Token).WaitAsync(Timeout, Token)).ShouldSucceed();

        (await _server.PublishAsync(new DaemonStatusChanged(Status), Token)).ShouldSucceed();

        await delivered.Task.WaitAsync(Timeout, Token);
        _clientErrors.ShouldHaveSingleItem().Value.ShouldBeOfType<HandlerFailed>().Message.ShouldContain("handler bug");
        _client.Disconnected.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task report_a_peer_that_sends_a_malformed_frame_and_keep_serving()
    {
        _server.MapQuery((GetDaemonStatus _, CancellationToken _) => Succeed(Status));
        await StartAndConnectAsync();
        using var rogue = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await rogue.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), Token);
        await using var rogueStream = new NetworkStream(rogue);

        (await FrameCodec.WriteAsync(rogueStream, Encoding.UTF8.GetBytes("this is not json"), Token)).ShouldSucceed();

        (await _firstServerError.Task.WaitAsync(Timeout, Token)).Value.ShouldBeOfType<MalformedFrame>();
        (await _client.QueryAsync(new GetDaemonStatus(), Token).WaitAsync(Timeout, Token)).ShouldSucceed().ShouldBe(Status);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    public async Task restrict_the_socket_to_its_owner()
    {
        (await _server.StartAsync(Token)).ShouldSucceed();

        File.GetUnixFileMode(_socketPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task remove_the_socket_file_when_the_server_stops()
    {
        (await _server.StartAsync(Token)).ShouldSucceed();
        File.Exists(_socketPath).ShouldBeTrue();

        (await _server.StopAsync(Token)).ShouldSucceed();

        File.Exists(_socketPath).ShouldBeFalse();
    }
}

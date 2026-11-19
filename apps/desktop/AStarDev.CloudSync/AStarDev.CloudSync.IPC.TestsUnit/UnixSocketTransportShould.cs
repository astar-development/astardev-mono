namespace AStarDev.CloudSync.IPC.TestsUnit;

// A new instance is created per test, so every test gets its own socket path.
public sealed class UnixSocketTransportShould : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly DaemonStatusView Status = new(DaemonState.Running, "1.2.3", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private sealed record PingCommand(string Text) : ICommand;

    private readonly string _socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-ipc-{Guid.NewGuid():N}.sock");
    private readonly UnixSocketIpcServer _server;
    private readonly UnixSocketIpcClient _client;

    public UnixSocketTransportShould()
    {
        _server = new UnixSocketIpcServer(_socketPath);
        _client = new UnixSocketIpcClient(_socketPath);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _server.DisposeAsync();
        File.Delete(_socketPath);
    }

    private async Task StartAndConnectAsync()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);
        await _client.ConnectAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task serve_a_query_over_a_real_socket()
    {
        _server.MapQuery((GetDaemonStatus _, CancellationToken _) => Task.FromResult(Status));
        await StartAndConnectAsync();

        var result = await _client.QueryAsync(new GetDaemonStatus(), TestContext.Current.CancellationToken).WaitAsync(Timeout);

        result.ShouldBe(Status);
    }

    [Fact]
    public async Task push_events_to_a_subscriber()
    {
        var received = new TaskCompletionSource<DaemonStatusChanged>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.OnEvent<DaemonStatusChanged>(received.SetResult);
        await StartAndConnectAsync();
        await _client.SubscribeAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout);

        await _server.PublishAsync(new DaemonStatusChanged(Status), TestContext.Current.CancellationToken);

        (await received.Task.WaitAsync(Timeout)).ShouldBe(new DaemonStatusChanged(Status));
    }

    [Fact]
    public async Task tell_the_client_when_the_server_goes_away()
    {
        await StartAndConnectAsync();
        await _client.SubscribeAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout);
        _client.Disconnected.IsCompleted.ShouldBeFalse();

        await _server.DisposeAsync();

        await _client.Disconnected.WaitAsync(Timeout);
        await Should.ThrowAsync<IOException>(async () =>
            await _client.QueryAsync(new GetDaemonStatus(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task send_a_command_and_return_its_outcome()
    {
        _server.MapCommand((PingCommand command, CancellationToken _) =>
            Task.FromResult(new CommandResult(CommandOutcome.Accepted, command.Text)));
        await StartAndConnectAsync();

        var result = await _client.SendAsync(new PingCommand("hello"), TestContext.Current.CancellationToken).WaitAsync(Timeout);

        result.ShouldBe(new CommandResult(CommandOutcome.Accepted, "hello"));
    }

    [Fact]
    public async Task report_a_handler_failure_to_the_caller()
    {
        _server.MapQuery((GetDaemonStatus _, CancellationToken _) =>
            Task.FromException<DaemonStatusView>(new InvalidOperationException("boom")));
        await StartAndConnectAsync();

        var failure = await Should.ThrowAsync<IpcException>(async () =>
            await _client.QueryAsync(new GetDaemonStatus(), TestContext.Current.CancellationToken).WaitAsync(Timeout));
        failure.Message.ShouldContain("boom");

        // an unmapped message type is also reported rather than left to hang
        await Should.ThrowAsync<IpcException>(async () =>
            await _client.SendAsync(new PingCommand("unmapped"), TestContext.Current.CancellationToken).WaitAsync(Timeout));
    }

    [Fact]
    public async Task restrict_the_socket_to_its_owner()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);

        File.GetUnixFileMode(_socketPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task remove_the_socket_file_when_the_server_stops()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);
        File.Exists(_socketPath).ShouldBeTrue();

        await _server.DisposeAsync();

        File.Exists(_socketPath).ShouldBeFalse();
    }
}

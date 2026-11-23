using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class IpcHostedServiceShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IpcHostedService Create(FakeIpcServer server)
    {
        var services = DaemonTestHost.CreateWith(new StubCloudProvider()).Services;

        return new IpcHostedService(
            server,
            services.GetRequiredService<ConnectAccountHandler>(),
            services.GetRequiredService<DisconnectAccountHandler>(),
            services.GetRequiredService<AccountsQuery>(),
            services.GetRequiredService<RootSummaryQuery>(),
            services.GetRequiredService<DriveInfoQuery>(),
            services.GetRequiredService<RemoteFoldersQuery>(),
            services.GetRequiredService<CreateSyncJobHandler>(),
            services.GetRequiredService<AddSyncScopeHandler>(),
            services.GetRequiredService<RemoveSyncScopeHandler>(),
            services.GetRequiredService<SyncJobsQuery>(),
            services.GetRequiredService<SyncScopesQuery>(),
            services.GetRequiredService<StartSyncHandler>(),
            services.GetRequiredService<StopSyncHandler>(),
            services.GetRequiredService<ActivitiesQuery>(),
            services.GetRequiredService<ConflictsQuery>(),
            services.GetRequiredService<DeadLettersQuery>(),
            services.GetRequiredService<RetryOperationHandler>(),
            services.GetRequiredService<IgnoreOperationHandler>(),
            services.GetRequiredService<ResolveConflictHandler>(),
            services.GetRequiredService<SettingsQuery>(),
            services.GetRequiredService<SetSettingHandler>(),
            services.GetRequiredService<SyncStatusQuery>(),
            services.GetRequiredService<DashboardSnapshotQuery>(),
            services.GetRequiredService<HistoryQuery>(),
            services.GetRequiredService<LogsQuery>(),
            services.GetRequiredService<TracesQuery>(),
            services.GetRequiredService<DiagnosticsQuery>(),
            services.GetRequiredService<MetricsQuery>(),
            services.GetRequiredService<ProvidersQuery>(),
            services.GetRequiredService<AuditQuery>(),
            services.GetRequiredService<ExportQuery>(),
            TimeProvider.System,
            NullLogger<IpcHostedService>.Instance);
    }

    [Fact]
    public async Task stop_the_server_when_stopped()
    {
        var server = new FakeIpcServer([]);
        var service = Create(server);

        await service.StopAsync(Token);

        server.Stopped.ShouldBeTrue();
    }

    [Fact]
    public async Task honour_cancellation_when_starting()
    {
        var service = Create(new FakeIpcServer([]));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.StartAsync(source.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_stopping()
    {
        var service = Create(new FakeIpcServer([]));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => service.StopAsync(source.Token));
    }

    [Fact]
    public async Task return_a_startup_error_for_the_accept_connections_stage_when_the_server_cannot_listen()
    {
        var service = Create(new FakeIpcServer([], new ListenFailed("address in use")));

        var result = await service.ListenAsync(Token);

        var error = result.Value.ShouldBeOfType<Fail<StartupError>>().Error;
        error.StageName.ShouldBe("Accept connections");
        error.Cause.ShouldContain("address in use");
    }

    [Fact]
    public async Task return_a_startup_error_for_every_kind_of_ipc_failure()
    {
        IpcError[] failures =
        [
            new ConnectionFailed("a"), new NotConnected(), new ConnectionLost("b"), new FrameTooLarge(2, 1), new MalformedFrame("c"), new HandlerFailed("d"), new ListenFailed("e")
        ];

        foreach (var failure in failures)
        {
            var service = Create(new FakeIpcServer([], failure));

            var result = await service.ListenAsync(Token);

            result.Value.ShouldBeOfType<Fail<StartupError>>().Error.Cause.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task return_success_once_the_server_is_listening()
    {
        var ran = new List<string>();
        var service = Create(new FakeIpcServer(ran));

        var result = await service.ListenAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        ran.ShouldBe(["ipc"]);
    }

    [Fact]
    public async Task fail_the_host_start_with_the_startup_error_when_the_server_cannot_listen()
    {
        var service = Create(new FakeIpcServer([], new ListenFailed("address in use")));

        var exception = await Should.ThrowAsync<StartupFailedException>(() => service.StartAsync(Token));

        exception.Error.StageName.ShouldBe("Accept connections");
        exception.Error.Cause.ShouldContain("address in use");
    }
}

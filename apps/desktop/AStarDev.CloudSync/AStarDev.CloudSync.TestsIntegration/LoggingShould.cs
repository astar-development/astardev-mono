using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class LoggingShould
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task carry_correlation_ids_on_operation_logs()
    {
        var logs = new CapturingLoggerProvider();
        var executor = new ScriptedOperationExecutor(_ => new Ok<Unit>(default));
        await using var host = await CreateHostAsync(logs, executor);
        var job = await AddJobAsync(host);
        var operation = Queued(job, 0);
        await host.Services.GetRequiredService<IOperationStore>().EnqueueAsync(operation, Token);

        await host.Services.GetRequiredService<OperationScheduler>().RunOnceAsync(Token);

        var entries = OperationLogs(logs).Where(entry => entry.EventId is 1001 or 1002).ToList();
        entries.Count.ShouldBe(2);
        foreach (var entry in entries)
        {
            entry.ScopeValue("OperationId").ShouldBe(operation.Id.Value.ToString());
            entry.ScopeValue("ItemId").ShouldBe(operation.ItemId.Value.ToString());
            entry.ScopeValue("SyncJobId").ShouldBe(job.Value.ToString());
        }
    }

    [Fact]
    public async Task log_each_operation_state_transition_with_the_right_level()
    {
        var logs = new CapturingLoggerProvider();
        var outcomes = new Dictionary<int, Result<Unit, ProviderError>>
        {
            [0] = new Ok<Unit>(default),
            [1] = new Fail<ProviderError>(new Transient("network dropped")),
            [2] = new Fail<ProviderError>(new Permanent("forbidden")),
            [3] = new Fail<ProviderError>(new AuthenticationFailed("expired"))
        };
        var ranks = new Dictionary<ItemId, int>();
        var executor = new ScriptedOperationExecutor(operation => outcomes[ranks[operation.ItemId]]);
        await using var host = await CreateHostAsync(logs, executor);
        var job = await AddJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        foreach (var rank in outcomes.Keys)
        {
            var operation = Queued(job, rank);
            ranks[operation.ItemId] = rank;
            await store.EnqueueAsync(operation, Token);
        }

        var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        foreach (var _ in outcomes) await scheduler.RunOnceAsync(Token);

        var transitions = OperationLogs(logs).ToLookup(entry => entry.EventId, entry => entry.Level);
        transitions[1001].ShouldAllBe(level => level == LogLevel.Debug);
        transitions[1001].Count().ShouldBe(4);
        transitions[1002].ShouldBe([LogLevel.Information]);
        transitions[1003].ShouldBe([LogLevel.Warning]);
        transitions[1005].ShouldBe([LogLevel.Error]);
        transitions[1006].ShouldBe([LogLevel.Error]);
    }

    [Fact]
    public async Task include_the_error_code_on_operation_failures()
    {
        var logs = new CapturingLoggerProvider();
        var outcomes = new Dictionary<int, Result<Unit, ProviderError>>
        {
            [0] = new Fail<ProviderError>(new RateLimited(TimeSpan.FromSeconds(30))),
            [1] = new Fail<ProviderError>(new Transient("network dropped")),
            [2] = new Fail<ProviderError>(new Permanent("forbidden")),
            [3] = new Fail<ProviderError>(new AuthenticationFailed("expired"))
        };
        var ranks = new Dictionary<ItemId, int>();
        var executor = new ScriptedOperationExecutor(operation => outcomes[ranks[operation.ItemId]]);
        await using var host = await CreateHostAsync(logs, executor);
        var job = await AddJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        foreach (var rank in outcomes.Keys)
        {
            var operation = Queued(job, rank);
            ranks[operation.ItemId] = rank;
            await store.EnqueueAsync(operation, Token);
        }

        var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        foreach (var _ in outcomes) await scheduler.RunOnceAsync(Token);

        var messages = OperationLogs(logs).ToLookup(entry => entry.EventId, entry => entry.Message);
        messages[1004].Single().ShouldContain("CS-PORTS-0001");
        messages[1003].Single().ShouldContain("CS-PORTS-0002");
        messages[1005].Single().ShouldContain("CS-PORTS-0004");
        messages[1006].Single().ShouldContain("CS-PORTS-0003");
    }

    [Fact]
    public async Task log_rate_limit_as_a_warning()
    {
        var logs = new CapturingLoggerProvider();
        var executor = new ScriptedOperationExecutor(_ => new Fail<ProviderError>(new RateLimited(TimeSpan.FromSeconds(30))));
        await using var host = await CreateHostAsync(logs, executor);
        var job = await AddJobAsync(host);
        await host.Services.GetRequiredService<IOperationStore>().EnqueueAsync(Queued(job, 0), Token);

        await host.Services.GetRequiredService<OperationScheduler>().RunOnceAsync(Token);

        var entry = OperationLogs(logs).Single(candidate => candidate.EventId == 1004);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.State["RetryAtUtc"].ShouldBe(Start.AddSeconds(30));
    }

    [Fact]
    public async Task honour_cancellation_when_running_an_operation()
    {
        var logs = new CapturingLoggerProvider();
        await using var host = await CreateHostAsync(logs, new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<OperationScheduler>().RunOnceAsync(cancelled.Token));
    }

    private static IEnumerable<LogEntry> OperationLogs(CapturingLoggerProvider logs)
        => logs.Entries.Where(entry => entry.Category == typeof(OperationScheduler).FullName);

    private static async Task<DatabaseTestHost> CreateHostAsync(CapturingLoggerProvider logs, IOperationExecutor executor)
    {
        var host = DatabaseTestHost.Create(services =>
        {
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
            services.AddSingleton<TimeProvider>(new ManualTimeProvider(Start));
            services.AddSingleton(executor);
            services.AddSyncEngine();
        });
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        return host;
    }

    private static async Task<SyncJobId> AddJobAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Start);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, Start, Start);
        await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token);
        await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token);

        return job.Id;
    }

    private static Operation Queued(SyncJobId job, int ageRank)
        => new(new OperationId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, OperationStatus.Pending, 0, Start, null, Start.AddMinutes(ageRank));
}

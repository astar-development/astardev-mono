using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class ConflictResolverShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task record_a_resolution_in_the_journal()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cloudsync-resolver-{Guid.NewGuid():N}");
        using var host = CreateHost(directory);
        try
        {
            var conflict = await SeedConflictAsync(host);

            var outcomes = await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.Defer, Token);
            var entries = await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflict.Id.Value, Token);

            outcomes.Single().Value.ShouldBeOfType<Ok<Unit>>();
            var entry = entries.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldHaveSingleItem();
            entry.EventType.ShouldBe("ConflictDeferred");
            entry.PayloadJson.ShouldContain("Defer");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cloudsync-resolver-{Guid.NewGuid():N}");
        using var host = CreateHost(directory);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([new ConflictId(Guid.NewGuid())], ConflictResolution.Defer, cancelled.Token));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static IHost CreateHost(string directory)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = Path.Combine(directory, "cloudsync.db");
        builder.Configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:{nameof(TelemetryDatabaseOptions.Path)}"] = Path.Combine(directory, "telemetry.db");
        builder.Logging.ClearProviders();
        builder.Services.AddCloudSyncPersistence();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IConflictEventSink, SilentConflictEventSink>();
        builder.Services.AddSyncEngine();

        return builder.Build();
    }

    private static async Task<Conflict> SeedConflictAsync(IHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Merge, true, Stamp, Stamp);
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Open, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<IConflictStore>().AddAsync(conflict, Token)).Value.ShouldBeOfType<Ok<bool>>();

        return conflict;
    }

    private sealed class SilentConflictEventSink : IConflictEventSink
    {
        public Task DetectedAsync(Conflict conflict, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResolvedAsync(Conflict conflict, string resolution, DateTimeOffset resolvedUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

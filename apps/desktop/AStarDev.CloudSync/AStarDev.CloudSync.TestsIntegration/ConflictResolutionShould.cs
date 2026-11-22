using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class ConflictResolutionShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private readonly string localRoot = Path.Combine(Path.GetTempPath(), $"cloudsync-resolution-{Guid.NewGuid():N}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(localRoot)) Directory.Delete(localRoot, recursive: true);
    }

    [Fact]
    public async Task journal_every_decision()
    {
        var sink = new RecordingConflictEventSink();
        await using var host = CreateHost(sink);
        var job = await SeedConflictingItemAsync(host);

        await PlanAsync(host, job);
        await PlanAsync(host, job);

        var conflict = (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value.ShouldHaveSingleItem();
        var entry = (await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflict.Id.Value, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldHaveSingleItem();
        entry.EventType.ShouldBe("ConflictDetected");
        entry.OccurredUtc.ShouldBe(conflict.CreatedUtc);
        entry.PayloadJson.ShouldContain("ModifyModify");
        entry.PayloadJson.ShouldContain(conflict.ItemId.Value.ToString());
        sink.Detected.Select(detected => detected.Id).ShouldBe([conflict.Id]);

        var resolver = host.Services.GetRequiredService<ConflictResolver>();
        (await resolver.ResolveAsync([conflict.Id], ConflictResolution.Defer, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        (await resolver.ResolveAsync([conflict.Id], ConflictResolution.KeepLocal, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();

        var entries = (await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflict.Id.Value, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value;
        entries.Select(journaled => journaled.EventType).ShouldBe(["ConflictDetected", "ConflictDeferred", "ConflictResolved"]);
        entries[1].PayloadJson.ShouldContain("Defer");
        entries[2].PayloadJson.ShouldContain("KeepLocal");
        sink.Resolved.ShouldHaveSingleItem().Resolution.ShouldBe("KeepLocal");
    }

    [Fact]
    public async Task journal_a_decision_for_each_choice_and_announce_only_resolutions()
    {
        var sink = new RecordingConflictEventSink();
        await using var host = CreateRealHost(new FakeCloudProvider(10), sink);
        var job = await SeedConflictsAsync(host, 4);
        await PlanAsync(host, job);
        var conflicts = await OpenAsync(host, job);
        var choices = new[] { ConflictResolution.KeepLocal, ConflictResolution.KeepRemote, ConflictResolution.KeepBoth, ConflictResolution.Defer };

        for (var index = 0; index < choices.Length; index++)
            (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflicts[index].Id], choices[index], Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();

        for (var index = 0; index < choices.Length; index++)
        {
            var entries = (await host.Services.GetRequiredService<IEventJournal>().ListAsync(conflicts[index].Id.Value, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value;
            entries.Select(entry => entry.EventType).ShouldBe(["ConflictDetected", choices[index] == ConflictResolution.Defer ? "ConflictDeferred" : "ConflictResolved"]);
            entries[1].PayloadJson.ShouldContain(choices[index].ToString());
        }

        sink.Resolved.Select(resolved => resolved.Resolution).Order().ShouldBe(["KeepBoth", "KeepLocal", "KeepRemote"]);
        (await OpenAsync(host, job)).Select(open => open.Id).ShouldBe([conflicts[3].Id]);
    }

    [Fact]
    public async Task keep_both_preserves_both_versions()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateRealHost(provider, new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();

        (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.KeepBoth, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        await RunAllAsync(host);

        var copy = Directory.GetFiles(localRoot, "f0 (Conflict - *).txt").ShouldHaveSingleItem();
        (await File.ReadAllTextAsync(copy, Token)).ShouldBe("local-0");
        (await File.ReadAllTextAsync(Path.Combine(localRoot, "f0.txt"), Token)).ShouldBe("remote-0");
        var upload = provider.Uploads.ShouldHaveSingleItem();
        upload.Path.Value.ShouldBe(Path.GetFileName(copy));
        System.Text.Encoding.UTF8.GetString(upload.Content).ShouldBe("local-0");
        (await OpenAsync(host, job)).ShouldBeEmpty();
        (await Operations(host)).ShouldAllBe(operation => operation.Status == OperationStatus.Completed);
    }

    [Fact]
    public async Task keep_local_by_uploading_the_local_file()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateRealHost(provider, new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();

        (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.KeepLocal, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        await RunAllAsync(host);

        var upload = provider.Uploads.ShouldHaveSingleItem();
        upload.Path.Value.ShouldBe("f0.txt");
        System.Text.Encoding.UTF8.GetString(upload.Content).ShouldBe("local-0");
        (await File.ReadAllTextAsync(Path.Combine(localRoot, "f0.txt"), Token)).ShouldBe("local-0");
        (await Operations(host)).Select(operation => operation.Priority).ShouldBe([OperationPriority.UserInitiated]);
        (await StatusAsync(host, conflict.Id)).ShouldBe(ConflictStatus.Resolved);
    }

    [Fact]
    public async Task keep_remote_by_downloading_the_remote_file()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateRealHost(provider, new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();

        (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.KeepRemote, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        await RunAllAsync(host);

        (await File.ReadAllTextAsync(Path.Combine(localRoot, "f0.txt"), Token)).ShouldBe("remote-0");
        provider.Uploads.ShouldBeEmpty();
        Directory.GetFiles(localRoot).Length.ShouldBe(1);
        (await StatusAsync(host, conflict.Id)).ShouldBe(ConflictStatus.Resolved);
    }

    [Fact]
    public async Task defer_without_changing_the_item_or_queueing_work()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateRealHost(provider, new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();

        (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.Defer, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        await RunAllAsync(host);

        (await OpenAsync(host, job)).Select(open => open.Id).ShouldBe([conflict.Id]);
        (await Operations(host)).ShouldBeEmpty();
        provider.Uploads.ShouldBeEmpty();
        (await File.ReadAllTextAsync(Path.Combine(localRoot, "f0.txt"), Token)).ShouldBe("local-0");
    }

    [Fact]
    public async Task mark_a_deferred_conflict_as_deferred_keep_it_listed_and_not_raise_it_again()
    {
        var sink = new RecordingConflictEventSink();
        await using var host = CreateRealHost(new FakeCloudProvider(10), sink);
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();

        (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.Defer, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        await PlanAsync(host, job);

        (await StatusAsync(host, conflict.Id)).ShouldBe(ConflictStatus.Deferred);
        (await OpenAsync(host, job)).ShouldHaveSingleItem().Id.ShouldBe(conflict.Id);
        sink.Detected.ShouldHaveSingleItem();
        sink.Resolved.ShouldBeEmpty();
    }

    [Fact]
    public async Task resolve_a_deferred_conflict()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateRealHost(provider, new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();
        var resolver = host.Services.GetRequiredService<ConflictResolver>();
        (await resolver.ResolveAsync([conflict.Id], ConflictResolution.Defer, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();

        (await resolver.ResolveAsync([conflict.Id], ConflictResolution.KeepLocal, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();

        (await StatusAsync(host, conflict.Id)).ShouldBe(ConflictStatus.Resolved);
        (await OpenAsync(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task resolve_many_conflicts_with_one_choice_and_report_each()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateRealHost(provider, new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 3);
        await PlanAsync(host, job);
        var conflicts = await OpenAsync(host, job);
        var resolver = host.Services.GetRequiredService<ConflictResolver>();
        (await resolver.ResolveAsync([conflicts[0].Id], ConflictResolution.Defer, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();
        (await resolver.ResolveAsync([conflicts[0].Id], ConflictResolution.KeepRemote, Token)).Single().Value.ShouldBeOfType<Ok<Unit>>();

        var results = await resolver.ResolveAsync([.. conflicts.Select(conflict => conflict.Id)], ConflictResolution.KeepLocal, Token);
        await RunAllAsync(host);

        results.Select(result => result.Value is Ok<Unit>).ShouldBe([false, true, true]);
        results[0].Value.ShouldBeOfType<Fail<ResolutionError>>().Error.Value.ShouldBeOfType<ConflictNotOpen>();
        provider.Uploads.Select(upload => upload.Path.Value).Order().ShouldBe(["f1.txt", "f2.txt"]);
        (await OpenAsync(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation_when_resolving()
    {
        await using var host = CreateRealHost(new FakeCloudProvider(10), new RecordingConflictEventSink());
        var job = await SeedConflictsAsync(host, 1);
        await PlanAsync(host, job);
        var conflict = (await OpenAsync(host, job)).Single();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([conflict.Id], ConflictResolution.KeepBoth, cancelled.Token));

        (await OpenAsync(host, job)).ShouldHaveSingleItem();
        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task refuse_an_unknown_conflict()
    {
        await using var host = CreateRealHost(new FakeCloudProvider(10), new RecordingConflictEventSink());
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        var result = (await host.Services.GetRequiredService<ConflictResolver>().ResolveAsync([new ConflictId(Guid.NewGuid())], ConflictResolution.KeepBoth, Token)).Single();

        result.Value.ShouldBeOfType<Fail<ResolutionError>>().Error.Value.ShouldBeOfType<ResolutionStoreFailed>().Error.Value.ShouldBeOfType<ConflictNotFound>();
    }

    [Fact]
    public async Task keep_entries_oldest_first_per_aggregate()
    {
        await using var host = CreateHost(new RecordingConflictEventSink());
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        var aggregate = Guid.NewGuid();
        var later = new JournalEntry(Guid.NewGuid(), Stamp.AddMinutes(1), "ConflictResolved", aggregate, "{}");
        var earlier = new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", aggregate, "{}");
        var other = new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", Guid.NewGuid(), "{}");

        await journal.AppendAsync(later, Token);
        await journal.AppendAsync(earlier, Token);
        await journal.AppendAsync(other, Token);
        var listed = await journal.ListAsync(aggregate, Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldBe([earlier, later]);
    }

    [Fact]
    public async Task honour_cancellation_when_appending_to_the_journal()
    {
        await using var host = CreateHost(new RecordingConflictEventSink());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var entry = new JournalEntry(Guid.NewGuid(), Stamp, "ConflictDetected", Guid.NewGuid(), "{}");

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IEventJournal>().AppendAsync(entry, cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_listing_the_journal()
    {
        await using var host = CreateHost(new RecordingConflictEventSink());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IEventJournal>().ListAsync(Guid.NewGuid(), cancelled.Token));
    }

    private static DatabaseTestHost CreateRealHost(FakeCloudProvider provider, RecordingConflictEventSink sink)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<ICloudProvider>(provider);
            services.AddSingleton<IConflictEventSink>(sink);
            services.AddSyncEngine();
        });

    private static DatabaseTestHost CreateHost(RecordingConflictEventSink sink)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)) as IOperationExecutor);
            services.AddSingleton<IConflictEventSink>(sink);
            services.AddSyncEngine();
        });

    private static async Task<SyncJobId> SeedConflictingItemAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var remote = new ProviderItem(new RemoteItemId("r1"), new RelativePath("a.txt"), ItemType.File, 10, Stamp);
        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job.Id, Root), [remote], CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var local = new LocalSnapshot(new RelativePath("a.txt"), 20, Stamp.AddHours(1), null, new ContentFingerprint("fp20"));
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job.Id, [local], [], Stamp.AddHours(2), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var item = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.Single().ItemId;
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await known.StageAsync(job.Id, operation, item, new KnownSnapshot(new RelativePath("a.txt"), 5, Stamp.AddDays(-1), null, null), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await known.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task PlanAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>();

    private async Task<SyncJobId> SeedConflictsAsync(DatabaseTestHost host, int count)
    {
        Directory.CreateDirectory(localRoot);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", localRoot, Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var provider = (FakeCloudProvider)host.Services.GetRequiredService<ICloudProvider>();
        var remotes = new List<ProviderItem>();
        var locals = new List<LocalSnapshot>();

        for (var index = 0; index < count; index++)
        {
            var local = $"local-{index}";
            var remote = $"remote-{index}";
            var full = Path.Combine(localRoot, $"f{index}.txt");
            await File.WriteAllTextAsync(full, local, Token);
            File.SetLastWriteTimeUtc(full, Stamp.UtcDateTime.AddHours(1));
            provider.Contents[new RemoteItemId($"r{index}")] = System.Text.Encoding.UTF8.GetBytes(remote);
            remotes.Add(new ProviderItem(new RemoteItemId($"r{index}"), new RelativePath($"f{index}.txt"), ItemType.File, remote.Length, Stamp.AddHours(2)));
            locals.Add(new LocalSnapshot(new RelativePath($"f{index}.txt"), local.Length, Stamp.AddHours(1), null, new ContentFingerprint($"fp-local-{index}")));
        }

        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job.Id, Root), remotes, CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job.Id, locals, [], Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var items = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();

        foreach (var item in items)
        {
            var operation = new OperationId(Guid.NewGuid());
            (await known.StageAsync(job.Id, operation, item.ItemId, new KnownSnapshot(item.Path, 5, Stamp.AddDays(-1), null, null), Token)).Value.ShouldBeOfType<Ok<Unit>>();
            (await known.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        }

        return job.Id;
    }

    private static async Task<IReadOnlyList<Conflict>> OpenAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value;

    private static async Task<ConflictStatus> StatusAsync(DatabaseTestHost host, ConflictId id)
        => (await host.Services.GetRequiredService<IConflictStore>().GetAsync(id, Token)).Value.ShouldBeOfType<Ok<Conflict>>().Value.Status;

    private static async Task<IReadOnlyList<Operation>> Operations(DatabaseTestHost host)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

    private static async Task RunAllAsync(DatabaseTestHost host)
    {
        await using var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        while ((await scheduler.RunOnceAsync(Token)).Value is Ok<bool> { Value: true }) { }
    }
}

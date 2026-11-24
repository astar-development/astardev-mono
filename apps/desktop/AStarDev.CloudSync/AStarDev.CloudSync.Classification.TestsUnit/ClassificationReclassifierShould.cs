using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Classification.TestsUnit.SyncTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationReclassifierShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task link_files_that_were_stored_before_the_vocabulary_existed()
    {
        await using var host = await HostWithFilesAsync("1", "a/one.jpg", "a/two.jpg", "b/three.txt");

        var outcome = await host.Services.GetRequiredService<IClassificationReclassifier>().StartAsync(new Progress<ReclassifyProgress>(), Token);

        outcome.Value.ShouldBeOfType<ReclassifyCompleted>().FilesClassified.ShouldBe(3);
        (await FilesFor(host, "jpg")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task report_progress_after_every_batch()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.jpg", "a/2.jpg", "a/3.jpg", "a/4.jpg", "a/5.jpg");
        var reports = new List<long>();

        await host.Services.GetRequiredService<IClassificationReclassifier>().StartAsync(new SyncProgress(report => reports.Add(report.FilesDone)), Token);

        reports.ShouldBe([2, 4, 5]);
    }

    [Fact]
    public async Task stop_on_cancellation_and_give_the_same_links_on_rerun()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.jpg", "a/2.jpg", "a/3.jpg", "a/4.jpg", "a/5.jpg");
        var reclassifier = host.Services.GetRequiredService<IClassificationReclassifier>();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);

        await Should.ThrowAsync<OperationCanceledException>(() => reclassifier.StartAsync(new SyncProgress(_ => cancel.Cancel()), cancel.Token));
        var rerun = await reclassifier.StartAsync(new Progress<ReclassifyProgress>(), Token);

        rerun.Value.ShouldBeOfType<ReclassifyCompleted>();
        (await FilesFor(host, "jpg")).Count.ShouldBe(5);
    }

    [Fact]
    public async Task return_already_running_for_a_second_start()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.jpg", "a/2.jpg", "a/3.jpg");
        var reclassifier = host.Services.GetRequiredService<IClassificationReclassifier>();
        ReclassifyOutcome? second = null;

        await reclassifier.StartAsync(new SyncProgress(_ => second ??= reclassifier.StartAsync(new Progress<ReclassifyProgress>(), Token).GetAwaiter().GetResult()), Token);

        second.ShouldNotBeNull().Value.ShouldBeOfType<ReclassifyAlreadyRunning>();
    }

    [Fact]
    public async Task keep_a_link_the_user_chose()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.txt");
        var terms = (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value;
        var jpg = terms.Single(term => term.Text == "jpg");
        var item = (await host.Services.GetRequiredService<IClassifiableFileSource>().GetPageAsync(null, 10, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<ClassifiableFile>>>().Value.Single();
        await host.Services.GetRequiredService<IFileClassificationStore>().AssignUserLinkAsync(item.ItemId, jpg.Id, Token);

        await host.Services.GetRequiredService<IClassificationReclassifier>().StartAsync(new Progress<ReclassifyProgress>(), Token);

        (await FilesFor(host, "jpg")).ShouldBe([item.ItemId]);
    }

    [Fact]
    public async Task record_the_run_and_skip_the_next_start_if_stale()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.jpg");
        var reclassifier = host.Services.GetRequiredService<IClassificationReclassifier>();

        await reclassifier.StartAsync(new Progress<ReclassifyProgress>(), Token);
        var again = await reclassifier.StartIfStaleAsync(new Progress<ReclassifyProgress>(), Token);

        again.Value.ShouldBeOfType<ReclassifyNotNeeded>();
        (await host.Services.GetRequiredService<IReclassifyRunStore>().GetLastCompletedAsync(Token)).Value.ShouldBeOfType<Ok<Option<ReclassifyRun>>>().Value.Value.ShouldBeOfType<Some<ReclassifyRun>>().Value.SeedVersion.ShouldBe("1");
    }

    [Fact]
    public async Task run_when_no_run_was_ever_recorded()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.jpg");

        var outcome = await host.Services.GetRequiredService<IClassificationReclassifier>().StartIfStaleAsync(new Progress<ReclassifyProgress>(), Token);

        outcome.Value.ShouldBeOfType<ReclassifyCompleted>();
    }

    [Fact]
    public async Task run_again_when_a_newer_seed_was_applied_since_the_last_run()
    {
        var source = new SwitchingSeedSource(FixedSeedSource.Seed("1", new SeedTerm("jpg", "Colour")));
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(source));
        var seeder = host.Services.GetRequiredService<ClassificationSeeder>();
        var reclassifier = host.Services.GetRequiredService<IClassificationReclassifier>();
        await seeder.SeedAsync(Token);
        await reclassifier.StartAsync(new Progress<ReclassifyProgress>(), Token);
        source.Seed = FixedSeedSource.Seed("2", new SeedTerm("jpg", "Colour"), new SeedTerm("png", "Colour"));
        await seeder.SeedAsync(Token);

        var outcome = await reclassifier.StartIfStaleAsync(new Progress<ReclassifyProgress>(), Token);

        outcome.Value.ShouldBeOfType<ReclassifyCompleted>();
    }

    [Fact]
    public async Task fail_without_recording_when_the_vocabulary_cannot_be_loaded()
    {
        var failing = new CountingVocabularyProvider(new Fail<ClassificationError>(new StorageFailed("down")), 0);
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationVocabularyProvider>(failing));

        var outcome = await host.Services.GetRequiredService<IClassificationReclassifier>().StartAsync(new Progress<ReclassifyProgress>(), Token);

        outcome.Value.ShouldBeOfType<ReclassifyFailed>();
        (await host.Services.GetRequiredService<IReclassifyRunStore>().GetLastCompletedAsync(Token)).Value.ShouldBeOfType<Ok<Option<ReclassifyRun>>>().Value.Value.ShouldBeOfType<None<ReclassifyRun>>();
    }

    [Fact]
    public async Task wait_until_idle_when_no_run_is_active()
    {
        await using var host = await HostWithFilesAsync("1", "a/1.jpg");

        await host.Services.GetRequiredService<IClassificationReclassifier>().WaitUntilIdleAsync(Token);
    }

    private static async Task<ClassificationTestHost> HostWithFilesAsync(string seedVersion, params string[] paths)
    {
        var seed = FixedSeedSource.Seed(seedVersion, new SeedTerm("jpg", "Colour"), new SeedTerm("txt", "Colour"));
        var host = await ClassificationTestHost.CreateAsync(Token, services =>
        {
            services.AddSingleton<IClassificationSeedSource>(new FixedSeedSource(new Ok<ClassificationSeed>(seed)));
            services.AddSingleton(new ReclassifyOptions(2, TimeSpan.Zero));
        });
        await SaveFilesAsync(host, Token, paths);
        await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        return host;
    }

    private static async Task<IReadOnlyList<ItemId>> FilesFor(ClassificationTestHost host, string term)
    {
        var terms = (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value;

        return (await host.Services.GetRequiredService<IFileClassificationStore>().GetItemsForTermAsync(terms.Single(candidate => candidate.Text == term).Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<ItemId>>>().Value;
    }

    private sealed class SyncProgress(Action<ReclassifyProgress> onReport) : IProgress<ReclassifyProgress>
    {
        public void Report(ReclassifyProgress value) => onReport(value);
    }

    private sealed class SwitchingSeedSource(ClassificationSeed seed) : IClassificationSeedSource
    {
        public ClassificationSeed Seed { get; set; } = seed;

        public Task<Result<ClassificationSeed, InvalidSeed>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<Result<ClassificationSeed, InvalidSeed>>(new Ok<ClassificationSeed>(Seed));
    }
}

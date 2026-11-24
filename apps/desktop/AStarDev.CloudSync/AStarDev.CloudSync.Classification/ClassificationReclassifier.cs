using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Classification;

/// <summary>Reclassifies every stored file a page at a time. When the vocabulary changes during a run, the run starts over, up to three times, so it ends on the current vocabulary.</summary>
/// <param name="files">Lists the stored files.</param>
/// <param name="links">Stores the automatic links.</param>
/// <param name="vocabulary">Supplies the vocabulary and reports when it changed.</param>
/// <param name="classifier">Classifies one path.</param>
/// <param name="runs">Remembers the last finished run.</param>
/// <param name="state">Holds the applied seed version.</param>
/// <param name="options">Page size and pause between batches.</param>
/// <param name="time">Waits between batches and stamps the run.</param>
/// <param name="logger">Reports the outcome without paths or terms.</param>
internal sealed class ClassificationReclassifier(IClassifiableFileSource files, IFileClassificationStore links, IClassificationVocabularyProvider vocabulary, FileClassifier classifier, IReclassifyRunStore runs, IClassificationStateStore state, ReclassifyOptions options, TimeProvider time, ILogger<ClassificationReclassifier> logger) : IClassificationReclassifier, IDisposable
{
    private const int MaxPasses = 3;

    private readonly SemaphoreSlim gate = new(1, 1);

    /// <inheritdoc />
    public void Dispose() => gate.Dispose();

    /// <inheritdoc />
    public async Task<ReclassifyOutcome> StartAsync(IProgress<ReclassifyProgress> progress, CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return new ReclassifyAlreadyRunning();

        try
        {
            return Report(await RunAsync(progress, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ReclassifyOutcome> StartIfStaleAsync(IProgress<ReclassifyProgress> progress, CancellationToken cancellationToken)
    {
        var applied = await state.GetSeedVersionAsync(cancellationToken).ConfigureAwait(false);
        var last = await runs.GetLastCompletedAsync(cancellationToken).ConfigureAwait(false);
        if (applied.Value is not Ok<Option<string>> seed || last.Value is not Ok<Option<ReclassifyRun>> run) return new ReclassifyFailed(nameof(StorageFailed));

        var current = run.Value.Value is Some<ReclassifyRun> recorded && seed.Value.Value is Some<string> version && recorded.Value.SeedVersion == version.Value;

        return current ? new ReclassifyNotNeeded() : await StartAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        gate.Release();
    }

    private ReclassifyOutcome Report(ReclassifyOutcome outcome)
    {
        switch (outcome.Value)
        {
            case ReclassifyCompleted completed:
                ClassificationLog.ReclassifyCompleted(logger, completed.FilesClassified);
                break;
            case ReclassifyFailed failed:
                ClassificationLog.ReclassifyFailed(logger, failed.Reason);
                break;
        }

        return outcome;
    }

    private async Task<ReclassifyOutcome> RunAsync(IProgress<ReclassifyProgress> progress, CancellationToken cancellationToken)
    {
        for (var pass = 1; pass <= MaxPasses; pass++)
        {
            var generation = vocabulary.Generation;
            var outcome = await PassAsync(progress, cancellationToken).ConfigureAwait(false);
            if (outcome.Value is not ReclassifyCompleted || vocabulary.Generation == generation || pass == MaxPasses) return await RecordAsync(outcome, cancellationToken).ConfigureAwait(false);
        }

        return new ReclassifyFailed("No pass ran");
    }

    private async Task<ReclassifyOutcome> RecordAsync(ReclassifyOutcome outcome, CancellationToken cancellationToken)
    {
        if (outcome.Value is not ReclassifyCompleted completed) return outcome;

        var applied = await state.GetSeedVersionAsync(cancellationToken).ConfigureAwait(false);
        var seedVersion = applied.Value is Ok<Option<string>> { Value.Value: Some<string> version } ? version.Value : string.Empty;
        var recorded = await runs.RecordCompletedAsync(new ReclassifyRun(time.GetUtcNow(), completed.FilesClassified, seedVersion), cancellationToken).ConfigureAwait(false);

        return recorded.Value is Ok<Unit> ? outcome : new ReclassifyFailed(nameof(StorageFailed));
    }

    private async Task<ReclassifyOutcome> PassAsync(IProgress<ReclassifyProgress> progress, CancellationToken cancellationToken)
    {
        var loaded = await vocabulary.GetAsync(cancellationToken).ConfigureAwait(false);
        if (loaded.Value is not Ok<ClassificationVocabulary> current) return new ReclassifyFailed("The vocabulary could not be loaded");

        long done = 0;
        ItemId? after = null;
        while (true)
        {
            var page = await files.GetPageAsync(after, options.PageSize, cancellationToken).ConfigureAwait(false);
            if (page.Value is not Ok<IReadOnlyList<ClassifiableFile>> batch) return new ReclassifyFailed("The files could not be read");

            var updates = batch.Value.Select(file => new ItemTermLinks(file.ItemId, [.. classifier.Classify(file.Path, current.Value).Candidates.Select(candidate => candidate.Term.Id)])).ToList();
            if ((await links.ReplaceAutoLinksAsync(updates, cancellationToken).ConfigureAwait(false)).Value is Fail<ClassificationError>) return new ReclassifyFailed("The links could not be written");

            done += batch.Value.Count;
            progress.Report(new ReclassifyProgress(done));
            if (batch.Value.Count < options.PageSize) return new ReclassifyCompleted(done);

            after = batch.Value[^1].ItemId;
            await Task.Delay(options.BatchDelay, time, cancellationToken).ConfigureAwait(false);
        }
    }
}

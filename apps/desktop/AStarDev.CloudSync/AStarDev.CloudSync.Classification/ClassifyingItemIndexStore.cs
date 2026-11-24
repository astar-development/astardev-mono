using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Classification;

/// <summary>
/// Decorates the item index so each file written by a crawl or delta page is classified once the page is stored. The inner result is always returned unchanged:
/// a classification failure is logged and never reaches the sync. A deleted item loses its links with the item, so nothing is done for it here.
/// </summary>
/// <param name="inner">The store that holds the index.</param>
/// <param name="classifier">Classifies one path.</param>
/// <param name="vocabulary">Supplies the vocabulary and reports when it changed while a page was classified.</param>
/// <param name="links">Stores the automatic links.</param>
/// <param name="logger">Reports failures without paths, terms or exception messages.</param>
internal sealed class ClassifyingItemIndexStore(IItemIndexStore inner, FileClassifier classifier, IClassificationVocabularyProvider vocabulary, IFileClassificationStore links, ILogger<ClassifyingItemIndexStore> logger) : IItemIndexStore
{
    private const int MaxAttempts = 3;

    /// <inheritdoc />
    public Task<Result<CrawlCheckpoint, StoreError>> GetCheckpointAsync(SyncScope scope, CancellationToken cancellationToken) => inner.GetCheckpointAsync(scope, cancellationToken);

    /// <inheritdoc />
    public Task<Result<Unit, StoreError>> ResetScopeAsync(SyncScope scope, CancellationToken cancellationToken) => inner.ResetScopeAsync(scope, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<Unit, StoreError>> SavePageAsync(SyncScope scope, IReadOnlyList<ProviderItem> items, CrawlCheckpoint checkpoint, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var result = await inner.SavePageAsync(scope, items, checkpoint, observedUtc, cancellationToken).ConfigureAwait(false);
        if (result.Value is Ok<Unit>) await ClassifyAsync(scope.SyncJobId, items, cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<Unit, StoreError>> ApplyDeltaPageAsync(SyncScope scope, IReadOnlyList<ProviderChange> changes, DeltaToken? newToken, DateTimeOffset observedUtc, CancellationToken cancellationToken)
    {
        var result = await inner.ApplyDeltaPageAsync(scope, changes, newToken, observedUtc, cancellationToken).ConfigureAwait(false);
        if (result.Value is Ok<Unit>) await ClassifyAsync(scope.SyncJobId, changes.Where(change => change.Kind != ChangeKind.Deleted).Select(change => change.Item).OfType<ProviderItem>(), cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task ClassifyAsync(SyncJobId jobId, IEnumerable<ProviderItem> items, CancellationToken cancellationToken)
    {
        var files = LastFilePerRemoteId(items);
        if (files.Count == 0) return;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var generation = vocabulary.Generation;
            await ClassifyOnceAsync(jobId, files, cancellationToken).ConfigureAwait(false);
            if (vocabulary.Generation == generation) return;
        }
    }

    private async Task ClassifyOnceAsync(SyncJobId jobId, List<ProviderItem> files, CancellationToken cancellationToken)
    {
        var loaded = await vocabulary.GetAsync(cancellationToken).ConfigureAwait(false);
        if (loaded.Value is not Ok<ClassificationVocabulary> current)
        {
            ClassificationLog.VocabularyUnavailable(logger);

            return;
        }

        var updates = files.Select(file => new RemoteItemTermLinks(file.RemoteItemId, [.. classifier.Classify(file.Path, current.Value).Candidates.Select(candidate => candidate.Term.Id)])).ToList();
        var stored = await links.ReplaceAutoLinksAsync(jobId, updates, cancellationToken).ConfigureAwait(false);
        if (stored.Value is Fail<ClassificationError> failed) ClassificationLog.LinksNotUpdated(logger, failed.Error.Value.GetType().Name);
    }

    private static List<ProviderItem> LastFilePerRemoteId(IEnumerable<ProviderItem> items) =>
        [.. items.Where(item => item.ItemType == ItemType.File).Reverse().DistinctBy(item => item.RemoteItemId).Reverse()];
}

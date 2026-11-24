using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Classification;

/// <summary>Writes the shipped seed into the stores when its version is new. Terms already stored are left as they are, so a user's edit survives a new seed version.</summary>
/// <param name="seedSource">Loads the seed.</param>
/// <param name="state">Remembers the applied version.</param>
/// <param name="categories">Stores categories.</param>
/// <param name="terms">Stores terms.</param>
/// <param name="vocabulary">Told to reload once the stores changed.</param>
/// <param name="logger">Reports the outcome without terms or paths.</param>
internal sealed class ClassificationSeeder(IClassificationSeedSource seedSource, IClassificationStateStore state, ICategoryStore categories, ITermStore terms, IClassificationVocabularyProvider vocabulary, ILogger<ClassificationSeeder> logger)
{
    /// <summary>Applies the seed when the stored version differs. Never throws for an expected failure; cancellation propagates.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What happened; also logged.</returns>
    internal async Task<SeedOutcome> SeedAsync(CancellationToken cancellationToken)
    {
        var outcome = await RunAsync(cancellationToken).ConfigureAwait(false);
        Report(outcome);

        return outcome;
    }

    private void Report(SeedOutcome outcome)
    {
        switch (outcome.Value)
        {
            case SeedApplied:
                ClassificationLog.SeedApplied(logger);
                break;
            case SeedAlreadyCurrent:
                ClassificationLog.SeedAlreadyCurrent(logger);
                break;
            case SeedFailed failed:
                ClassificationLog.SeedFailed(logger, failed.Reason);
                break;
        }
    }

    private async Task<SeedOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var loaded = await seedSource.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (loaded.Value is not Ok<ClassificationSeed> seed) return new SeedFailed(nameof(InvalidSeed));

        var applied = await state.GetSeedVersionAsync(cancellationToken).ConfigureAwait(false);
        if (applied.Value is Fail<ClassificationError> failed) return new SeedFailed(failed.Error.Value.GetType().Name);
        if (applied.Value is Ok<Option<string>> { Value.Value: Some<string> current } && current.Value == seed.Value.Version) return new SeedAlreadyCurrent();

        return await ApplyAsync(seed.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SeedOutcome> ApplyAsync(ClassificationSeed seed, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, CategoryId>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in seed.Categories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var stored = await categories.GetOrAddAsync(new Category(new CategoryId(Guid.NewGuid()), name), cancellationToken).ConfigureAwait(false);
            if (stored.Value is not Ok<Category> category) return new SeedFailed(nameof(StorageFailed));

            ids[name] = category.Value.Id;
        }

        var seedTerms = ToTerms(seed, ids);
        if (seedTerms.Value is not Ok<List<Term>> toStore) return new SeedFailed(nameof(InvalidSeed));

        var written = await terms.AddMissingAsync(toStore.Value, cancellationToken).ConfigureAwait(false);
        vocabulary.Invalidate();
        if (written.Value is Fail<ClassificationError> writeFailure) return new SeedFailed(writeFailure.Error.Value.GetType().Name);

        var recorded = await state.SetSeedVersionAsync(seed.Version, cancellationToken).ConfigureAwait(false);

        return recorded.Value is Ok<Unit> ? new SeedApplied() : new SeedFailed(nameof(StorageFailed));
    }

    private static Result<List<Term>, InvalidSeed> ToTerms(ClassificationSeed seed, Dictionary<string, CategoryId> ids)
    {
        var result = new List<Term>(seed.Terms.Count);
        foreach (var seedTerm in seed.Terms)
        {
            if (!ids.TryGetValue(seedTerm.CategoryName, out var categoryId)) return new Fail<InvalidSeed>(new InvalidSeed("A term names a category the seed does not define"));
            if (ClassificationText.NormaliseTerm(seedTerm.Text) is Some<string> text) result.Add(new Term(new TermId(Guid.NewGuid()), text.Value, categoryId, TermSource.Seed, TermStatus.Active));
        }

        return new Ok<List<Term>>([.. result.DistinctBy(term => (term.Text, term.CategoryId))]);
    }
}

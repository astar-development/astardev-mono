using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where classification remembers facts about itself between runs, such as which version of the shipped seed was applied.
/// Failure is a <see cref="ClassificationError"/> value, never an exception; cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IClassificationStateStore
{
    /// <summary>Reads the version of the shipped seed that was last applied.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The version, or none when no seed was ever applied. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<Option<string>, ClassificationError>> GetSeedVersionAsync(CancellationToken cancellationToken);

    /// <summary>Records the version of the shipped seed that was applied, replacing any earlier one. Safe to call twice.</summary>
    /// <param name="version">The applied version.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the earlier version.</param>
    /// <returns>Success, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> SetSeedVersionAsync(string version, CancellationToken cancellationToken);
}

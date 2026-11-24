using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Lists every stored file a page at a time, so a run over 700,000 files never holds them all in memory. Failure is a <see cref="ClassificationError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IClassifiableFileSource
{
    /// <summary>Reads the next page of files in a stable order.</summary>
    /// <param name="after">The last file of the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">The most files to return.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>Up to <paramref name="pageSize"/> files after <paramref name="after"/>; fewer than that means the last page, and empty means none are left. A file added or removed between calls may be missed or seen once. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<ClassifiableFile>, ClassificationError>> GetPageAsync(ItemId? after, int pageSize, CancellationToken cancellationToken);
}

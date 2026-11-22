using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where what the local observer saw on disk is kept, as items and <c>Local</c> snapshots. It only records; it decides nothing. A deletion is recorded as a <c>Local</c> snapshot whose size is <see cref="DeletedSize"/>, so history is kept.
/// Failure is a <see cref="StoreError"/> value, never an exception; cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ILocalObservationStore
{
    /// <summary>The <see cref="LocalSnapshot.Size"/> value stored for an observation that a file is gone. Never a real file size.</summary>
    public const long DeletedSize = -1;

    /// <summary>Lists the files whose latest local observation says they exist.</summary>
    /// <param name="jobId">The job whose items to read.</param>
    /// <param name="subtree">A file, or a folder with everything below it. An empty path means the whole job. A folder's name matches that folder's files only, never a sibling that merely starts with the same letters.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>One snapshot per file, each with the <see cref="LocalSnapshot.Identity"/> that was recorded, null when none was; empty when nothing was observed; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<LocalSnapshot>, StoreError>> ListPresentAsync(SyncJobId jobId, RelativePath subtree, CancellationToken cancellationToken);

    /// <summary>Records what the local disk showed, all or nothing.</summary>
    /// <param name="jobId">The job the files belong to.</param>
    /// <param name="present">Files seen. A path not stored for the job gets a new item with no remote id; a stored path, including one created by a remote crawl, gets a new <c>Local</c> snapshot. Recording the same snapshot twice adds two snapshots; the caller avoids that by comparing with <see cref="ListPresentAsync"/>.</param>
    /// <param name="deleted">Paths no longer on disk. A path with no stored item, or whose latest local observation is already a deletion, is ignored, so calling twice is safe.</param>
    /// <param name="observedUtc">When the disk was looked at. Snapshots of one item are kept in strictly increasing observation order even when this value repeats.</param>
    /// <param name="cancellationToken">Cancelling before the write completes records nothing.</param>
    /// <returns>Success, <see cref="SyncJobNotFound"/> when the job does not exist, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> RecordAsync(SyncJobId jobId, IReadOnlyList<LocalSnapshot> present, IReadOnlyList<RelativePath> deleted, DateTimeOffset observedUtc, CancellationToken cancellationToken);
}

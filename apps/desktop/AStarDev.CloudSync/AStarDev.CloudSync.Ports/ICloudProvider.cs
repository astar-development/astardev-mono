using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// The only way the sync engine talks to a cloud provider. Failure is a <see cref="ProviderError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// Later slices add the remaining members of doc 09 (deltas, download, upload, rename, delete).
/// </summary>
public interface ICloudProvider
{
    /// <summary>Signs in and reports which account was connected. Call before any other member.</summary>
    /// <returns>The connected account, or <see cref="AuthenticationFailed"/> when the credentials are refused.</returns>
    Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Lists the remote items under the scope's root as they are now, one page at a time.</summary>
    /// <param name="scope">The remote folder to list.</param>
    /// <param name="cancellationToken">Cancelling abandons the listing and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>One page of items, or the reason the listing failed. Match <see cref="ProviderError"/> to decide whether to retry.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.GetSnapshotAsync(new SyncScope(rootId), cancellationToken);
    /// var delay = page switch
    /// {
    ///     Fail&lt;ProviderError&gt; { Error: RateLimited limited } => limited.RetryAfter,
    ///     _ => TimeSpan.Zero
    /// };
    /// </code>
    /// </example>
    Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken);
}

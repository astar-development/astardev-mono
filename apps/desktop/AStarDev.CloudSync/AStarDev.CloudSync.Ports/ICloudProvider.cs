using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// The only way the sync engine talks to a cloud provider. Failure is a <see cref="ProviderError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ICloudProvider
{
    /// <summary>What this provider can do, which decides the actions the UI offers for its accounts. Fixed for the life of the provider; empty means only what every provider does.</summary>
    IReadOnlyList<ProviderFeature> Features { get; }

    /// <summary>Signs in and reports which account was connected. Call before any other member.</summary>
    /// <returns>The connected account, or <see cref="AuthenticationFailed"/> when the credentials are refused.</returns>
    Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Signs in again from the stored sign-in, without opening a browser. Call at start-up for every stored account.</summary>
    /// <param name="accountIdentifier">The provider's id for the account, as <see cref="ProviderAccount.AccountIdentifier"/> reported it.</param>
    /// <param name="cancellationToken">Cancelling abandons the attempt and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>The account, <see cref="AuthenticationFailed"/> when the stored sign-in is missing or no longer accepted and the user must sign in again, or <see cref="Transient"/> when the provider could not be reached.</returns>
    Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken);

    /// <summary>Forgets the stored sign-in of an account, so nothing about it remains on this machine.</summary>
    /// <param name="accountIdentifier">The provider's id for the account.</param>
    /// <param name="cancellationToken">Cancelling abandons the removal.</param>
    /// <returns>Success, also when there was nothing stored; or the reason the stored sign-in could not be removed. Safe to call twice.</returns>
    Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken);

    /// <summary>Lists every file and folder directly under one remote folder, following the provider's paging until the last page.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="parent">The folder to list. Pass the provider's root id to list the top of the drive.</param>
    /// <param name="cancellationToken">Cancelling abandons the listing and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>The children in the order the provider reports them, empty for an empty folder; or the first <see cref="ProviderError"/> met, in which case nothing is returned from earlier pages. Items inside subfolders are not included.</returns>
    Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(string accountIdentifier, RemoteItemId parent, CancellationToken cancellationToken);

    /// <summary>Lists the remote items under one remote root as they are now, one page at a time.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="root">The remote folder to list.</param>
    /// <param name="resumeToken"><see langword="null"/> to start at the first page, or the <see cref="ProviderSnapshotPage.ResumeToken"/> of the previous page to continue after it, also in a later run of the process.</param>
    /// <param name="cancellationToken">Cancelling abandons the listing and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>One page of items, or the reason the listing failed. A page with no <see cref="ProviderSnapshotPage.ResumeToken"/> is the last. Match <see cref="ProviderError"/> to decide whether to retry; the same call can be repeated.</returns>
    /// <example>
    /// <code>
    /// var page = await provider.GetSnapshotAsync(rootId, null, cancellationToken);
    /// var delay = page switch
    /// {
    ///     Fail&lt;ProviderError&gt; { Error: RateLimited limited } => limited.RetryAfter,
    ///     _ => TimeSpan.Zero
    /// };
    /// </code>
    /// </example>
    Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(string accountIdentifier, RemoteItemId root, string? resumeToken, CancellationToken cancellationToken);

    /// <summary>Lists what changed under one remote root since a delta token was issued, one page at a time.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="root">The remote folder the token was issued for.</param>
    /// <param name="token">The bookmark from <see cref="ProviderSnapshotPage.DeltaToken"/> or the last <see cref="ProviderDeltaPage.DeltaToken"/>.</param>
    /// <param name="resumeToken"><see langword="null"/> to start at the first page, or the <see cref="ProviderDeltaPage.ResumeToken"/> of the previous page to continue after it.</param>
    /// <param name="cancellationToken">Cancelling abandons the call and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>One page of changes, or the reason it failed. The last page carries the new <see cref="ProviderDeltaPage.DeltaToken"/>. <see cref="InvalidDeltaToken"/> means the token is spent: do not retry, list the root again. The same call can be repeated after other failures.</returns>
    Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(string accountIdentifier, RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken);

    /// <summary>Opens the content of one remote file for reading.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="itemId">The file to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the call and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>A stream of the content that the caller must dispose, or <see cref="Permanent"/> when the file is missing or forbidden. Nothing is read until the stream is; a failure while reading surfaces as an <see cref="IOException"/> from the stream. The same call can be repeated.</returns>
    Task<Result<Stream, ProviderError>> DownloadAsync(string accountIdentifier, RemoteItemId itemId, CancellationToken cancellationToken);

    /// <summary>Puts a file's content at a remote path, replacing what is there, and gives the remote copy the request's modified time.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="request">What to send and where.</param>
    /// <param name="cancellationToken">Cancelling abandons the upload and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>Success, or the reason the upload failed. Small files go in one request, larger ones in chunks of one upload session. A failed upload leaves the earlier remote content in place; repeating the call starts the upload again from the beginning.</returns>
    Task<Result<Unit, ProviderError>> UploadAsync(string accountIdentifier, UploadRequest request, CancellationToken cancellationToken);

    /// <summary>Renames or moves a remote item to a new path.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="itemId">The item to move.</param>
    /// <param name="path">The new path from the drive root. The folder it sits in must exist.</param>
    /// <param name="cancellationToken">Cancelling abandons the call and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>Success, also when the item is already at the path, or the reason the move failed.</returns>
    Task<Result<Unit, ProviderError>> RenameAsync(string accountIdentifier, RemoteItemId itemId, RelativePath path, CancellationToken cancellationToken);

    /// <summary>Deletes a remote item.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="itemId">The item to delete.</param>
    /// <param name="cancellationToken">Cancelling abandons the call and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>Success, also when the item is already gone, so calling twice is safe; or the reason the delete failed.</returns>
    Task<Result<Unit, ProviderError>> DeleteAsync(string accountIdentifier, RemoteItemId itemId, CancellationToken cancellationToken);

    /// <summary>Reports how much storage the connected drive uses, has left and is allowed.</summary>
    /// <param name="accountIdentifier">The account whose drive is used, as <see cref="ProviderAccount.AccountIdentifier"/> reported it. An account that is not signed in is <see cref="AuthenticationFailed"/>; one account's call never carries another account's sign-in.</param>
    /// <param name="cancellationToken">Cancelling abandons the call and surfaces as <see cref="OperationCanceledException"/>, never as a <see cref="ProviderError"/>.</param>
    /// <returns>The drive facts, or the reason they could not be read. <see cref="ProviderDriveInfo.ItemCountEstimate"/> is none when the provider has no such figure. The same call can be repeated.</returns>
    Task<Result<ProviderDriveInfo, ProviderError>> GetDriveInfoAsync(string accountIdentifier, CancellationToken cancellationToken);
}

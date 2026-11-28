using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>OneDrive behind the <see cref="ICloudProvider"/> port: browser sign-in with a keyring token cache, root enumeration and item transfer through Graph.</summary>
internal sealed class OneDriveProvider(IOptions<OneDriveSettings> settings, ITokenCacheStore store, OneDriveAccounts accounts, TimeProvider time, ReauthenticationMarker marker, ILogger<OneDriveProvider> logger) : ICloudProvider
{
    private readonly TokenRefresher refresher = new(accounts, time);

    /// <inheritdoc/>
    public IReadOnlyList<ProviderFeature> Features { get; } = [ProviderFeature.SelectiveSync, ProviderFeature.DeltaSync];

    /// <inheritdoc/>
    /// <remarks>The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so. A successful sign-in lets later calls carry its access token.</remarks>
    public Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
        => ObserveAsync("Connect", null, null, ConnectCoreAsync, cancellationToken);

    private async Task<Result<ProviderAccount, ProviderError>> ConnectCoreAsync(CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveConnector(store, accounts, new MsalBrowserSignIn(options.Value)).ConnectAsync(cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so. Every account keeps its own access token, so reconnecting one never changes another.</remarks>
    public Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => ObserveAsync("Reconnect", accountIdentifier, null, token => ReconnectCoreAsync(accountIdentifier, token), cancellationToken);

    private async Task<Result<ProviderAccount, ProviderError>> ReconnectCoreAsync(string accountIdentifier, CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveReconnector(store, accounts, new MsalSilentSignIn(options.Value)).ReconnectAsync(accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>Removes only this account from the token cache, keeping the entry while another account is signed in, and drops its access token held in memory. The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so.</remarks>
    public Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => ObserveAsync("Disconnect", accountIdentifier, null, token => DisconnectCoreAsync(accountIdentifier, token), cancellationToken);

    private async Task<Result<Unit, ProviderError>> DisconnectCoreAsync(string accountIdentifier, CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveDisconnector(store, accounts, new MsalSignOut(options.Value)).DisconnectAsync(accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>
    /// Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.
    /// Returns one page of the whole tree under the root through Graph's delta endpoint, so the listing is recursive and flat; the resume token is a Graph next link. The last page carries the Graph delta link as the delta token. Graph reports no total, so <see cref="ProviderSnapshotPage.TotalItems"/> is <see langword="null"/>.
    /// Paths are built from parent ids and names, so they are relative to the drive root, keeping the scope folder, and correct where OneDrive Personal omits parent paths. A parent not yet seen is read from Graph, costing one request per unknown folder after a restart; an item whose parent Graph no longer has, or that lies outside <paramref name="root"/>, is left out, as is <paramref name="root"/> itself.
    /// </remarks>
    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(string accountIdentifier, RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("GetSnapshot", accountIdentifier, root.Value, token => accounts.For(accountIdentifier).Enumerator.GetSnapshotAsync(root, resumeToken, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// The token is a Graph delta link and the resume token a Graph next link, both used as the request URL. HTTP 410 is <see cref="InvalidDeltaToken"/>.
    /// Graph does not report renames, so a renamed or moved item arrives as <see cref="ChangeKind.Modified"/> with its new path. Paths are resolved as for <see cref="GetSnapshotAsync"/>.
    /// </remarks>
    public Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(string accountIdentifier, RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("GetDeltas", accountIdentifier, root.Value, call => accounts.For(accountIdentifier).Enumerator.GetDeltasAsync(root, token, resumeToken, call), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.</remarks>
    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(string accountIdentifier, RemoteItemId parent, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("ListChildren", accountIdentifier, parent.Value, token => accounts.For(accountIdentifier).Enumerator.ListChildrenAsync(parent, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Reads through Graph's content endpoint, which redirects to a pre-authenticated URL. A missing or forbidden item is <see cref="Permanent"/>.</remarks>
    public Task<Result<Stream, ProviderError>> DownloadAsync(string accountIdentifier, RemoteItemId itemId, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("Download", accountIdentifier, itemId.Value, token => accounts.For(accountIdentifier).Transfer.DownloadAsync(itemId, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Files up to 4 MiB go in one request followed by a request that sets the modified time; larger ones use an upload session with 10 MiB chunks, which sets it when the session opens. A failed chunk abandons the session, so repeating starts again from the beginning.</remarks>
    public Task<Result<Unit, ProviderError>> UploadAsync(string accountIdentifier, UploadRequest request, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("Upload", accountIdentifier, null, token => accounts.For(accountIdentifier).Transfer.UploadAsync(request, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Posts the folder to its parent's children with Graph's conflict behaviour <c>fail</c>; Graph's 409 means the name is taken, so the item at the path is read and its id returned when it is a folder. Graph answers 404, so <see cref="Permanent"/>, when the parent folder does not exist.</remarks>
    public Task<Result<RemoteItemId, ProviderError>> CreateFolderAsync(string accountIdentifier, RelativePath path, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("CreateFolder", accountIdentifier, null, token => accounts.For(accountIdentifier).Transfer.CreateFolderAsync(path, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Sends the new name and the new parent folder path in one request. Graph answers 404, so <see cref="Permanent"/>, when the parent folder does not exist.</remarks>
    public Task<Result<Unit, ProviderError>> RenameAsync(string accountIdentifier, RemoteItemId itemId, RelativePath path, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("Rename", accountIdentifier, itemId.Value, token => accounts.For(accountIdentifier).Transfer.RenameAsync(itemId, path, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>A 404 from Graph counts as success. Graph moves the item to the OneDrive recycle bin.</remarks>
    public Task<Result<Unit, ProviderError>> DeleteAsync(string accountIdentifier, RemoteItemId itemId, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("Delete", accountIdentifier, itemId.Value, token => accounts.For(accountIdentifier).Transfer.DeleteAsync(itemId, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Reads the quota of the account's drive from Graph. Graph has no item count, so <see cref="ProviderDriveInfo.ItemCountEstimate"/> is always none. Without a successful <see cref="ConnectAsync"/> first the result is <see cref="AuthenticationFailed"/>.</remarks>
    public Task<Result<ProviderDriveInfo, ProviderError>> GetDriveInfoAsync(string accountIdentifier, CancellationToken cancellationToken)
        => ObserveAuthorisedAsync("GetDriveInfo", accountIdentifier, null, accounts.For(accountIdentifier).Enumerator.GetDriveInfoAsync, cancellationToken);

    private Task<Result<T, ProviderError>> ObserveAuthorisedAsync<T>(string call, string accountId, string? remoteItemId, Func<CancellationToken, Task<Result<T, ProviderError>>> invoke, CancellationToken cancellationToken)
        => ObserveAsync(call, accountId, remoteItemId, token => AuthorisedAsync(accountId, invoke, token), cancellationToken);

    private async Task<Result<T, ProviderError>> AuthorisedAsync<T>(string accountId, Func<CancellationToken, Task<Result<T, ProviderError>>> invoke, CancellationToken cancellationToken)
    {
        Result<T, ProviderError> result = await refresher.EnsureFreshAsync(accountId, ReconnectCoreAsync, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<Unit> => await invoke(cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
        if (result is Fail<ProviderError> { Error: AuthenticationFailed }) await marker.MarkAsync(accountId, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<Result<T, ProviderError>> ObserveAsync<T>(string call, string? accountId, string? remoteItemId, Func<CancellationToken, Task<Result<T, ProviderError>>> invoke, CancellationToken cancellationToken)
    {
        using var scope = CorrelationScope.Begin(logger, accountId: accountId, remoteItemId: remoteItemId);
        var result = await invoke(cancellationToken).ConfigureAwait(false);
        if (result is Fail<ProviderError> fail) OneDriveLog.CallFailed(logger, call, fail.Error);

        return result;
    }
}

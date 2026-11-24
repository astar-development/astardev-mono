using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>OneDrive behind the <see cref="ICloudProvider"/> port: browser sign-in with a keyring token cache, root enumeration and item transfer through Graph.</summary>
internal sealed class OneDriveProvider(IOptions<OneDriveSettings> settings, ITokenCacheStore store, AccessTokenHolder holder, RootEnumerator enumerator, ItemTransfer transfer, ILogger<OneDriveProvider> logger) : ICloudProvider
{
    /// <inheritdoc/>
    public IReadOnlyList<ProviderFeature> Features { get; } = [ProviderFeature.SelectiveSync, ProviderFeature.DeltaSync];

    /// <inheritdoc/>
    /// <remarks>The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so. A successful sign-in lets later calls carry its access token.</remarks>
    public Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
        => ObserveAsync("Connect", null, null, ConnectCoreAsync, cancellationToken);

    private async Task<Result<ProviderAccount, ProviderError>> ConnectCoreAsync(CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveConnector(store, holder, new MsalBrowserSignIn(options.Value)).ConnectAsync(cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so. The token cache holds a single sign-in, so only one account can be reconnected at a time.</remarks>
    public Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => ObserveAsync("Reconnect", accountIdentifier, null, token => ReconnectCoreAsync(accountIdentifier, token), cancellationToken);

    private async Task<Result<ProviderAccount, ProviderError>> ReconnectCoreAsync(string accountIdentifier, CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveReconnector(store, holder, new MsalSilentSignIn(options.Value)).ReconnectAsync(accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>Removes the whole Secret Service entry, because the token cache holds a single sign-in, and drops the access token held in memory. Needs no client id.</remarks>
    public Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => ObserveAsync("Disconnect", accountIdentifier, null, token => new OneDriveDisconnector(store, holder).DisconnectAsync(token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.
    /// Returns one page of the whole tree under the root through Graph's delta endpoint, so the listing is recursive and flat; the resume token is a Graph next link. The last page carries the Graph delta link as the delta token. Graph reports no total, so <see cref="ProviderSnapshotPage.TotalItems"/> is <see langword="null"/>.
    /// Paths are built from parent ids and names, so they are relative to the drive root, keeping the scope folder, and correct where OneDrive Personal omits parent paths. A parent not yet seen is read from Graph, costing one request per unknown folder after a restart; an item whose parent Graph no longer has, or that lies outside <paramref name="root"/>, is left out, as is <paramref name="root"/> itself.
    /// </remarks>
    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
        => ObserveAsync("GetSnapshot", null, root.Value, token => enumerator.GetSnapshotAsync(root, resumeToken, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// The token is a Graph delta link and the resume token a Graph next link, both used as the request URL. HTTP 410 is <see cref="InvalidDeltaToken"/>.
    /// Graph does not report renames, so a renamed or moved item arrives as <see cref="ChangeKind.Modified"/> with its new path. Paths are resolved as for <see cref="GetSnapshotAsync"/>.
    /// </remarks>
    public Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken)
        => ObserveAsync("GetDeltas", null, root.Value, call => enumerator.GetDeltasAsync(root, token, resumeToken, call), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.</remarks>
    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(RemoteItemId parent, CancellationToken cancellationToken)
        => ObserveAsync("ListChildren", null, parent.Value, token => enumerator.ListChildrenAsync(parent, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Reads through Graph's content endpoint, which redirects to a pre-authenticated URL. A missing or forbidden item is <see cref="Permanent"/>.</remarks>
    public Task<Result<Stream, ProviderError>> DownloadAsync(RemoteItemId itemId, CancellationToken cancellationToken)
        => ObserveAsync("Download", null, itemId.Value, token => transfer.DownloadAsync(itemId, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Files up to 4 MiB go in one request followed by a request that sets the modified time; larger ones use an upload session with 10 MiB chunks, which sets it when the session opens. A failed chunk abandons the session, so repeating starts again from the beginning.</remarks>
    public Task<Result<Unit, ProviderError>> UploadAsync(UploadRequest request, CancellationToken cancellationToken)
        => ObserveAsync("Upload", null, null, token => transfer.UploadAsync(request, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Sends the new name and the new parent folder path in one request. Graph answers 404, so <see cref="Permanent"/>, when the parent folder does not exist.</remarks>
    public Task<Result<Unit, ProviderError>> RenameAsync(RemoteItemId itemId, RelativePath path, CancellationToken cancellationToken)
        => ObserveAsync("Rename", null, itemId.Value, token => transfer.RenameAsync(itemId, path, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>A 404 from Graph counts as success. Graph moves the item to the OneDrive recycle bin.</remarks>
    public Task<Result<Unit, ProviderError>> DeleteAsync(RemoteItemId itemId, CancellationToken cancellationToken)
        => ObserveAsync("Delete", null, itemId.Value, token => transfer.DeleteAsync(itemId, token), cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Reads the quota of the signed-in drive from Graph. Graph has no item count, so <see cref="ProviderDriveInfo.ItemCountEstimate"/> is always none. Without a successful <see cref="ConnectAsync"/> first the result is <see cref="AuthenticationFailed"/>.</remarks>
    public Task<Result<ProviderDriveInfo, ProviderError>> GetDriveInfoAsync(CancellationToken cancellationToken)
        => ObserveAsync("GetDriveInfo", null, null, enumerator.GetDriveInfoAsync, cancellationToken);

    private async Task<Result<T, ProviderError>> ObserveAsync<T>(string call, string? accountId, string? remoteItemId, Func<CancellationToken, Task<Result<T, ProviderError>>> invoke, CancellationToken cancellationToken)
    {
        using var scope = CorrelationScope.Begin(logger, accountId: accountId, remoteItemId: remoteItemId);
        var result = await invoke(cancellationToken).ConfigureAwait(false);
        if (result is Fail<ProviderError> fail) OneDriveLog.CallFailed(logger, call, fail.Error);

        return result;
    }
}

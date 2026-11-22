using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>OneDrive behind the <see cref="ICloudProvider"/> port: browser sign-in with a keyring token cache, and root enumeration through Graph.</summary>
internal sealed class OneDriveProvider(IOptions<OneDriveSettings> settings, ITokenCacheStore store, AccessTokenHolder holder, RootEnumerator enumerator) : ICloudProvider
{
    /// <inheritdoc/>
    /// <remarks>The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so. A successful sign-in lets later calls carry its access token.</remarks>
    public async Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveConnector(store, holder, new MsalBrowserSignIn(options.Value)).ConnectAsync(cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>The client id is read from the <c>OneDrive:ClientId</c> setting on every call; a missing one is an <see cref="AuthenticationFailed"/> value that says so. The token cache holds a single sign-in, so only one account can be reconnected at a time.</remarks>
    public async Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => OneDriveOptions.Create(settings.Value.ClientId) switch
        {
            Ok<OneDriveOptions> options => await new OneDriveReconnector(store, holder, new MsalSilentSignIn(options.Value)).ReconnectAsync(accountIdentifier, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };

    /// <inheritdoc/>
    /// <remarks>Removes the whole Secret Service entry, because the token cache holds a single sign-in, and drops the access token held in memory. Needs no client id.</remarks>
    public Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
        => new OneDriveDisconnector(store, holder).DisconnectAsync(cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.
    /// Lists one folder level and returns every page of it as a single page with no resume token and no delta token, so <paramref name="resumeToken"/> is ignored. Paging and deltas arrive with the delta slice.
    /// </remarks>
    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
        => enumerator.GetSnapshotAsync(root, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.</remarks>
    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(RemoteItemId parent, CancellationToken cancellationToken)
        => enumerator.ListChildrenAsync(parent, cancellationToken);
}

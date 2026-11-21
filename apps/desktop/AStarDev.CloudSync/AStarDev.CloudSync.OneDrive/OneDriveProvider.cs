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
    /// <remarks>Without a successful <see cref="ConnectAsync"/> first, Graph refuses the request and the result is <see cref="AuthenticationFailed"/>.</remarks>
    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken)
        => enumerator.GetSnapshotAsync(scope, cancellationToken);
}

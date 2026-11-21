using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Where the serialised MSAL token cache is kept between runs. An empty array means nothing is stored yet.</summary>
internal interface ITokenCacheStore
{
    /// <summary>Returns the stored cache, or an empty array when nothing is stored yet. A store that cannot be reached is a failure, never an empty cache.</summary>
    Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Replaces whatever is stored with <paramref name="cache"/>.</summary>
    Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken);
}

using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Where the serialised MSAL token cache is kept between runs. An empty array means nothing is stored yet.</summary>
internal interface ITokenCacheStore
{
    Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken);

    Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken);
}

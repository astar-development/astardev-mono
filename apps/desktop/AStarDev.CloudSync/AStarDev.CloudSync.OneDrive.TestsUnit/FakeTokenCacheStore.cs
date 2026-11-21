using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal sealed class FakeTokenCacheStore : ITokenCacheStore
{
    public byte[] Cache { get; set; } = [];

    public Result<byte[], ProviderError>? LoadResult { get; init; }

    public Result<Unit, ProviderError>? ClearResult { get; init; }

    public Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(LoadResult ?? new Ok<byte[]>(Cache));
    }

    public Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Cache = cache;

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    public Task<Result<Unit, ProviderError>> ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ClearResult is not null) return Task.FromResult(ClearResult.Value);
        Cache = [];

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }
}

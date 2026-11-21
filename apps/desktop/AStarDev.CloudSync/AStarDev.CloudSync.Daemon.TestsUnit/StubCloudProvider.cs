using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class StubCloudProvider : ICloudProvider
{
    public Result<ProviderAccount, ProviderError> ConnectResult { get; init; } = new Ok<ProviderAccount>(new ProviderAccount("id", "Ada"));

    public Result<ProviderSnapshotPage, ProviderError> SnapshotResult { get; init; } = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([]));

    public List<SyncScope> Scopes { get; } = [];

    public Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ConnectResult);
    }

    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Scopes.Add(scope);

        return Task.FromResult(SnapshotResult);
    }

    public static ProviderItem Item(string id) => new(new RemoteItemId(id), new RelativePath(id), ItemType.File, 1, DateTimeOffset.UnixEpoch);
}

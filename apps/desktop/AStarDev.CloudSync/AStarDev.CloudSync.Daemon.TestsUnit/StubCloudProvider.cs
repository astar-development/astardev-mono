using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class StubCloudProvider : ICloudProvider
{
    public Result<ProviderAccount, ProviderError> ConnectResult { get; init; } = new Ok<ProviderAccount>(new ProviderAccount("id", "Ada"));

    public Result<ProviderSnapshotPage, ProviderError> SnapshotResult { get; init; } = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([]));

    public Result<ProviderAccount, ProviderError> ReconnectResult { get; init; } = new Ok<ProviderAccount>(new ProviderAccount("id", "Ada"));

    public Result<Unit, ProviderError> DisconnectResult { get; init; } = new Ok<Unit>(default);

    public List<SyncScope> Scopes { get; } = [];

    public List<string> Reconnected { get; } = [];

    public List<string> Disconnected { get; } = [];

    public Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ConnectResult);
    }

    public Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reconnected.Add(accountIdentifier);

        return Task.FromResult(ReconnectResult);
    }

    public Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Disconnected.Add(accountIdentifier);

        return Task.FromResult(DisconnectResult);
    }

    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Scopes.Add(scope);

        return Task.FromResult(SnapshotResult);
    }

    public static ProviderItem Item(string id) => new(new RemoteItemId(id), new RelativePath(id), ItemType.File, 1, DateTimeOffset.UnixEpoch);
}

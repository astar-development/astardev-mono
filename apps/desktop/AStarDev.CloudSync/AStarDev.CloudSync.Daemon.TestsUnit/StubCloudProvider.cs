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

    public Result<IReadOnlyList<ProviderItem>, ProviderError> ChildrenResult { get; init; } = new Ok<IReadOnlyList<ProviderItem>>([]);

    public Result<ProviderDeltaPage, ProviderError> DeltaResult { get; init; } = new Ok<ProviderDeltaPage>(new ProviderDeltaPage([]));

    public int DeltaCalls { get; private set; }

    public List<RemoteItemId> Roots { get; } = [];

    public List<RemoteItemId> Parents { get; } = [];

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

    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Roots.Add(root);

        return Task.FromResult(SnapshotResult);
    }

    public Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeltaCalls++;

        return Task.FromResult(DeltaResult);
    }

    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(RemoteItemId parent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Parents.Add(parent);

        return Task.FromResult(ChildrenResult);
    }

    public static ProviderItem Folder(string id, string path) => new(new RemoteItemId(id), new RelativePath(path), ItemType.Folder, 0, DateTimeOffset.UnixEpoch);

    public static ProviderItem Item(string id) => new(new RemoteItemId(id), new RelativePath(id), ItemType.File, 1, DateTimeOffset.UnixEpoch);
}

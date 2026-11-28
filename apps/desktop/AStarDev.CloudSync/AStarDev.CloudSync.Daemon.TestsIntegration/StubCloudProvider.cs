using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsIntegration;

internal sealed class StubCloudProvider : ICloudProvider
{
    public IReadOnlyList<ProviderFeature> Features { get; init; } = [ProviderFeature.SelectiveSync, ProviderFeature.DeltaSync];

    public Result<ProviderAccount, ProviderError> ConnectResult { get; init; } = new Ok<ProviderAccount>(new ProviderAccount("id", "Ada"));

    public Result<ProviderSnapshotPage, ProviderError> SnapshotResult { get; init; } = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([]));

    public Result<ProviderAccount, ProviderError> ReconnectResult { get; init; } = new Ok<ProviderAccount>(new ProviderAccount("id", "Ada"));

    public Result<Unit, ProviderError> DisconnectResult { get; init; } = new Ok<Unit>(default);

    public Result<IReadOnlyList<ProviderItem>, ProviderError> ChildrenResult { get; init; } = new Ok<IReadOnlyList<ProviderItem>>([]);

    public Result<ProviderDeltaPage, ProviderError> DeltaResult { get; init; } = new Ok<ProviderDeltaPage>(new ProviderDeltaPage([]));

    public Result<ProviderDriveInfo, ProviderError> DriveInfoResult { get; init; } = new Ok<ProviderDriveInfo>(new ProviderDriveInfo(0, 0, 0, Option.None<long>()));

    public Result<RemoteItemId, ProviderError> CreateFolderResult { get; init; } = new Ok<RemoteItemId>(new RemoteItemId("created"));

    public List<(string Account, RelativePath Path)> CreatedFolders { get; } = [];

    public int DeltaCalls { get; private set; }

    public List<string> AccountsCalled { get; } = [];

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

    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(string accountIdentifier, RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountsCalled.Add(accountIdentifier);
        Roots.Add(root);

        return Task.FromResult(SnapshotResult);
    }

    public Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(string accountIdentifier, RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountsCalled.Add(accountIdentifier);
        DeltaCalls++;

        return Task.FromResult(DeltaResult);
    }

    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(string accountIdentifier, RemoteItemId parent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountsCalled.Add(accountIdentifier);
        Parents.Add(parent);

        return Task.FromResult(ChildrenResult);
    }

    public Task<Result<Stream, ProviderError>> DownloadAsync(string accountIdentifier, RemoteItemId itemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Stream, ProviderError>>(new Ok<Stream>(new MemoryStream()));
    }

    public Task<Result<Unit, ProviderError>> UploadAsync(string accountIdentifier, UploadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    public Task<Result<RemoteItemId, ProviderError>> CreateFolderAsync(string accountIdentifier, RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CreatedFolders.Add((accountIdentifier, path));

        return Task.FromResult(CreateFolderResult);
    }

    public Task<Result<Unit, ProviderError>> RenameAsync(string accountIdentifier, RemoteItemId itemId, RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    public Task<Result<ProviderDriveInfo, ProviderError>> GetDriveInfoAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountsCalled.Add(accountIdentifier);

        return Task.FromResult(DriveInfoResult);
    }

    public Task<Result<Unit, ProviderError>> DeleteAsync(string accountIdentifier, RemoteItemId itemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    public static ProviderItem Folder(string id, string path) => new(new RemoteItemId(id), new RelativePath(path), ItemType.Folder, 0, DateTimeOffset.UnixEpoch);

    public static ProviderItem Item(string id) => new(new RemoteItemId(id), new RelativePath(id), ItemType.File, 1, DateTimeOffset.UnixEpoch);
}

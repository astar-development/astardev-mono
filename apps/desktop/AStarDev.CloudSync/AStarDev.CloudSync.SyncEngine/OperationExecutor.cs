using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Carries out one claimed operation against the provider or the local disk. It only does the work the reconciler already decided on: it reads the item through the stores,
/// never writes sync state, and never replaces or deletes content the operation does not name. Baselines are committed by <see cref="KnownSnapshotCommitter"/> after success.
/// </summary>
internal sealed class OperationExecutor(ISyncJobStore jobStore, IItemStateStore itemStore, IKnownSnapshotStore knownStore, ICloudProvider provider, ILocalFileStore files) : IOperationExecutor
{
    /// <inheritdoc/>
    /// <remarks>
    /// Rebuilds the item's canonical state, as the planner saw it, and acts on it. A store that cannot be read is <see cref="Transient"/>.
    /// A local file that is locked, access-denied or outside the root is <see cref="Permanent"/>, so the operation dead-letters at once.
    /// A download, upload or rename whose item has vanished is <see cref="Permanent"/>; a delete whose item has vanished is success, so every operation is safe to repeat.
    /// </remarks>
    public async Task<Result<Unit, ProviderError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await jobStore.GetAsync(operation.SyncJobId, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<SyncJob> job => await ReadItemsAsync(operation, job.Value.LocalRootPath, cancellationToken).ConfigureAwait(false),
            Fail<StoreError> fail => Unreadable(fail.Error)
        };
    }

    private static Fail<ProviderError> Unreadable(StoreError error) => new(new Transient($"The state of the operation could not be read: {error.Value}"));

    private static Fail<ProviderError> Missing(string what) => new(new Permanent($"The operation's {what} is no longer known."));

    private static Result<Unit, ProviderError> Done() => new Ok<Unit>(default);

    private async Task<Result<Unit, ProviderError>> ReadItemsAsync(Operation operation, string root, CancellationToken cancellationToken)
        => await itemStore.ListAsync(operation.SyncJobId, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<IReadOnlyList<StoredItem>> rows => await ReadKnownAsync(operation, root, rows.Value, cancellationToken).ConfigureAwait(false),
            Fail<StoreError> fail => Unreadable(fail.Error)
        };

    private async Task<Result<Unit, ProviderError>> ReadKnownAsync(Operation operation, string root, IReadOnlyList<StoredItem> rows, CancellationToken cancellationToken)
        => await knownStore.ListAsync(operation.SyncJobId, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<IReadOnlyList<KnownEntry>> known => await RunAsync(operation.Type, root, CanonicalStateBuilder.Build(rows, known.Value).FirstOrDefault(state => state.ItemId == operation.ItemId), cancellationToken).ConfigureAwait(false),
            Fail<StoreError> fail => Unreadable(fail.Error)
        };

    private Task<Result<Unit, ProviderError>> RunAsync(OperationType type, string root, CanonicalItemState? state, CancellationToken cancellationToken)
    {
        if (type == OperationType.DownloadFile) return DownloadAsync(root, state, cancellationToken);
        if (type == OperationType.UploadFile) return UploadAsync(root, state, cancellationToken);
        if (type == OperationType.RenameRemote) return RenameRemoteAsync(state, cancellationToken);
        if (type == OperationType.RenameLocal) return Task.FromResult(RenameLocal(root, state));
        if (type == OperationType.DeleteRemote) return DeleteRemoteAsync(state, cancellationToken);

        return Task.FromResult(DeleteLocal(root, state));
    }

    private async Task<Result<Unit, ProviderError>> DownloadAsync(string root, CanonicalItemState? state, CancellationToken cancellationToken)
    {
        if (state?.Remote is not { } remote) return Missing("remote item");

        var target = state.Local?.Path ?? remote.Path;

        return await provider.DownloadAsync(remote.ItemId, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<Stream> content => await SaveAsync(root, target, content.Value, remote.ModifiedUtc, cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
    }

    private async Task<Result<Unit, ProviderError>> SaveAsync(string root, RelativePath target, Stream content, DateTimeOffset modifiedUtc, CancellationToken cancellationToken)
    {
        await using (content.ConfigureAwait(false)) return await files.WriteAsync(root, target, content, modifiedUtc, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Unit, ProviderError>> UploadAsync(string root, CanonicalItemState? state, CancellationToken cancellationToken)
    {
        if (state?.Local is not { } local) return Missing("local file");

        var source = files.Exists(root, local.Path) || state.Remote is null ? local.Path : state.Remote.Path;
        var request = state.Remote?.Path ?? local.Path;

        return files.OpenRead(root, source) switch
        {
            Ok<Stream> content => await SendAsync(new UploadRequest(request, content.Value, local.ModifiedUtc), cancellationToken).ConfigureAwait(false),
            Fail<ProviderError> fail => fail
        };
    }

    private async Task<Result<Unit, ProviderError>> SendAsync(UploadRequest request, CancellationToken cancellationToken)
    {
        await using (request.Content.ConfigureAwait(false)) return await provider.UploadAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private Task<Result<Unit, ProviderError>> RenameRemoteAsync(CanonicalItemState? state, CancellationToken cancellationToken)
        => state is { Remote: { } remote, Local: { } local }
            ? provider.RenameAsync(remote.ItemId, local.Path, cancellationToken)
            : Task.FromResult<Result<Unit, ProviderError>>(Missing("local or remote item"));

    private Result<Unit, ProviderError> RenameLocal(string root, CanonicalItemState? state)
        => state is { Remote: { } remote, Local: { } local }
            ? files.Move(root, local.Path, remote.Path)
            : Missing("local or remote item");

    private Task<Result<Unit, ProviderError>> DeleteRemoteAsync(CanonicalItemState? state, CancellationToken cancellationToken)
        => state?.Remote is { } remote ? provider.DeleteAsync(remote.ItemId, cancellationToken) : Task.FromResult(Done());

    private Result<Unit, ProviderError> DeleteLocal(string root, CanonicalItemState? state)
        => state?.Local is { } local ? files.Delete(root, local.Path) : Done();
}

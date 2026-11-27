using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Finds where a remote folder sits from the drive root, so a scope can say which local paths it owns.</summary>
internal static class RemoteFolderPaths
{
    private const string DriveRootAlias = "root";

    /// <summary>Walks the drive from its root, one folder listing at a time, until the folder is found.</summary>
    /// <param name="provider">Lists the folders.</param>
    /// <param name="folder">The provider's id for the folder; the drive root alias <c>root</c> is the drive root and needs no listing.</param>
    /// <param name="cancellationToken">Cancelling abandons the walk and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The folder's path from the drive root, empty text for the drive root; <see cref="Permanent"/> when no folder has the id; or the <see cref="ProviderError"/> of the listing that failed.</returns>
    public static async Task<Result<RelativePath, ProviderError>> FindAsync(ICloudProvider provider, string account, RemoteItemId folder, CancellationToken cancellationToken)
    {
        if (folder.Value == DriveRootAlias) return new Ok<RelativePath>(new RelativePath(string.Empty));

        var pending = new Queue<RemoteItemId>([new RemoteItemId(DriveRootAlias)]);
        var visited = new HashSet<RemoteItemId>();

        while (pending.TryDequeue(out var parent))
        {
            if (!visited.Add(parent)) continue;

            if (await provider.ListChildrenAsync(account, parent, cancellationToken) is not Ok<IReadOnlyList<ProviderItem>> listed) return new Fail<ProviderError>(new Permanent("The folder could not be listed."));

            var folders = listed.Value.Where(item => item.ItemType == ItemType.Folder).ToList();
            if (folders.FirstOrDefault(item => item.RemoteItemId == folder) is { } found) return new Ok<RelativePath>(found.Path);

            foreach (var child in folders) pending.Enqueue(child.RemoteItemId);
        }

        return new Fail<ProviderError>(new Permanent("The folder was not found in the drive."));
    }
}

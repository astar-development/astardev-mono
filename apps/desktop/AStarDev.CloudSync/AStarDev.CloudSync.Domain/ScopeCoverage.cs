namespace AStarDev.CloudSync.Domain;

/// <summary>Decides whether a path lies inside the scopes of a job. Pure: no I/O, no state.</summary>
public static class ScopeCoverage
{
    private const string DriveRootAlias = "root";

    /// <summary>Tells whether any scope covers a path.</summary>
    /// <param name="scopes">The scopes of the job; an empty list covers nothing, so nothing synchronises before the user selects a folder.</param>
    /// <param name="path">A file or folder path from the drive root.</param>
    /// <returns><see langword="true"/> when a scope is the drive root alias, or its non-empty path equals <paramref name="path"/> or is a folder above it. Names are compared case-sensitively and only whole path segments match, so <c>Docs</c> does not cover <c>Docs2</c>.</returns>
    public static bool Covers(IReadOnlyList<SyncScope> scopes, RelativePath path)
        => scopes.Any(scope => scope.RemoteRootId.Value == DriveRootAlias || Contains(scope.Path.Value, path.Value));

    /// <summary>Tells whether a folder is the path of a scope or lies above one, so it is partly synchronised although no scope covers it.</summary>
    /// <param name="scopes">The scopes of the job.</param>
    /// <param name="folder">A folder path from the drive root.</param>
    /// <returns><see langword="true"/> when some scope's non-empty path is <paramref name="folder"/> or below it.</returns>
    public static bool Touches(IReadOnlyList<SyncScope> scopes, RelativePath folder)
        => scopes.Any(scope => Contains(folder.Value, scope.Path.Value));

    private static bool Contains(string? folder, string? path)
        => !string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(path) && (path == folder || path.StartsWith($"{folder}/", StringComparison.Ordinal));
}

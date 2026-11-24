using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>
/// Works out where each item of a Graph page sits in the drive from the parent ids and names Graph sends, because OneDrive Personal leaves the parent path out of delta responses.
/// Folder paths are remembered between pages in a map capped at <see cref="MaxRememberedFolders"/>. The map is only a cache: a folder it does not hold is read from Graph and its own parents followed, so a restart or an eviction costs requests, never a wrong path.
/// </summary>
/// <param name="client">Authorised client whose base address is the Graph root.</param>
/// <param name="telemetry">Receives a span and a request count per parent fetched.</param>
internal sealed class PathResolver(HttpClient client, OneDriveTelemetry telemetry)
{
    private const string RootMarker = "root:";
    private const string DriveRootAlias = "root";
    private const int MaxRememberedFolders = 100_000;

    private readonly ConcurrentDictionary<string, string> folders = new();

    /// <summary>Resolves the drive-relative path of every live item below the sync root, so an item of a scope folder keeps the folder in its path.</summary>
    /// <param name="root">The sync root. Only items below it are returned; the drive root alias <c>root</c> admits every item.</param>
    /// <param name="items">One page of items in the order Graph sent them. A child may precede its parent, on this page or one not yet seen.</param>
    /// <param name="cancellationToken">Cancelling abandons the fetches and propagates as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The path from the drive root by item id. An item is left out when it is deleted, is the sync root itself, lies outside the sync root, or has a parent Graph no longer knows. Otherwise the <see cref="ProviderError"/> of the first parent fetch that failed.</returns>
    public async Task<Result<IReadOnlyDictionary<string, string>, ProviderError>> ResolveAsync(RemoteItemId root, IReadOnlyList<GraphDriveItem> items, CancellationToken cancellationToken)
    {
        var live = items.Where(item => item.Deleted is null).ToList();
        var pageFolders = live.Where(item => item.Folder is not null).GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.Last());
        var memo = new Dictionary<string, string?>();
        var before = pageFolders.Keys.Where(folders.ContainsKey).ToDictionary(id => id, id => folders[id]);

        if (await SettleFoldersAsync(pageFolders, memo, cancellationToken).ConfigureAwait(false) is Fail<ProviderError> failed) return failed;

        if (before.Any(pair => memo[pair.Key] != pair.Value))
        {
            folders.Clear();
            if (await SettleFoldersAsync(pageFolders, memo, cancellationToken).ConfigureAwait(false) is Fail<ProviderError> failedAgain) return failedAgain;
        }

        Result<string?, ProviderError> rootPath = root.Value == DriveRootAlias ? new Ok<string?>(string.Empty) : await FolderPathAsync(root.Value, pageFolders, memo, cancellationToken).ConfigureAwait(false);

        return rootPath switch
        {
            Fail<ProviderError> fail => fail,
            Ok<string?> { Value: null } => new Ok<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>()),
            Ok<string?> ok => await ResolveBelowAsync(ok.Value!, live, pageFolders, memo, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<Result<IReadOnlyDictionary<string, string>, ProviderError>> ResolveBelowAsync(string rootPath, List<GraphDriveItem> live, Dictionary<string, GraphDriveItem> pageFolders, Dictionary<string, string?> memo, CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, string>();

        foreach (var item in live)
        {
            var path = await ItemPathAsync(item, pageFolders, memo, cancellationToken).ConfigureAwait(false);

            if (path.Value is Fail<ProviderError> fail) return fail;
            if (path.Value is Ok<string?> { Value: { } full } && IsBelow(rootPath, full)) resolved[item.Id] = full;
        }

        return new Ok<IReadOnlyDictionary<string, string>>(resolved);
    }

    private async Task<Result<Unit, ProviderError>> SettleFoldersAsync(Dictionary<string, GraphDriveItem> pageFolders, Dictionary<string, string?> memo, CancellationToken cancellationToken)
    {
        memo.Clear();

        foreach (var id in pageFolders.Keys)
            if (await FolderPathAsync(id, pageFolders, memo, cancellationToken).ConfigureAwait(false) is Fail<ProviderError> fail) return fail;

        return new Ok<Unit>(default);
    }

    private async Task<Result<string?, ProviderError>> FolderPathAsync(string id, Dictionary<string, GraphDriveItem> pageFolders, Dictionary<string, string?> memo, CancellationToken cancellationToken)
    {
        if (memo.TryGetValue(id, out var known)) return new Ok<string?>(known);
        if (pageFolders.TryGetValue(id, out var listed)) return Remember(id, await ItemPathAsync(listed, pageFolders, memo, cancellationToken).ConfigureAwait(false), memo);
        if (folders.TryGetValue(id, out var cached)) return new Ok<string?>(cached);

        return await FetchAsync(id, cancellationToken).ConfigureAwait(false) switch
        {
            Fail<ProviderError> fail => fail,
            Ok<GraphDriveItem?> { Value: null } => Remember(id, new Ok<string?>(null), memo),
            Ok<GraphDriveItem?> ok => Remember(id, await ItemPathAsync(ok.Value!, pageFolders, memo, cancellationToken).ConfigureAwait(false), memo)
        };
    }

    private async Task<Result<string?, ProviderError>> ItemPathAsync(GraphDriveItem item, Dictionary<string, GraphDriveItem> pageFolders, Dictionary<string, string?> memo, CancellationToken cancellationToken)
    {
        if (item.Folder is not null && item.ParentReference?.Id is null && item.ParentReference?.Path is null) return new Ok<string?>(string.Empty);

        Result<string?, ProviderError> parent = FromPath(item.ParentReference?.Path) is { } fromPath
            ? new Ok<string?>(fromPath)
            : item.ParentReference?.Id is { } parentId
                ? await FolderPathAsync(parentId, pageFolders, memo, cancellationToken).ConfigureAwait(false)
                : new Ok<string?>(string.Empty);

        return parent switch
        {
            Fail<ProviderError> fail => fail,
            Ok<string?> { Value: null } => new Ok<string?>(null),
            Ok<string?> ok => new Ok<string?>(ok.Value!.Length == 0 ? item.Name : $"{ok.Value}/{item.Name}")
        };
    }

    private Result<string?, ProviderError> Remember(string id, Result<string?, ProviderError> path, Dictionary<string, string?> memo)
    {
        if (path.Value is not Ok<string?> ok) return path;

        memo[id] = ok.Value;
        if (ok.Value is null) return path;

        if (folders.Count >= MaxRememberedFolders) folders.Clear();
        folders[id] = ok.Value;

        return path;
    }

    private async Task<Result<GraphDriveItem?, ProviderError>> FetchAsync(string id, CancellationToken cancellationToken)
    {
        using var span = telemetry.StartPage();
        telemetry.RecordRequest();
        var outcome = await Try.RunAsync(token => GetItemAsync(id, token), cancellationToken).ConfigureAwait(false);
        Result<GraphDriveItem?, ProviderError> item = outcome switch
        {
            Success<Result<GraphDriveItem?, ProviderError>> success => success.Value,
            Failure<Result<GraphDriveItem?, ProviderError>> failure => new Fail<ProviderError>(AuthenticationErrors.From(failure.Exception))
        };

        if (item.Value is Fail<ProviderError>) telemetry.RecordFailure();

        return item;
    }

    private async Task<Result<GraphDriveItem?, ProviderError>> GetItemAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"me/drive/items/{Uri.EscapeDataString(id)}?$select=id,name,parentReference,folder", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound) return new Ok<GraphDriveItem?>(null);
        if (!response.IsSuccessStatusCode) return new Fail<ProviderError>(GraphErrors.From(response));

        return new Ok<GraphDriveItem?>(await response.Content.ReadFromJsonAsync<GraphDriveItem>(GraphJson.Options, cancellationToken).ConfigureAwait(false));
    }

    private static string? FromPath(string? parentPath)
    {
        var markerAt = parentPath?.IndexOf(RootMarker, StringComparison.Ordinal) ?? -1;

        return markerAt < 0 ? null : parentPath![(markerAt + RootMarker.Length)..].Trim('/');
    }

    private static bool IsBelow(string rootPath, string full)
        => full.Length > 0 && (rootPath.Length == 0 || full.StartsWith($"{rootPath}/", StringComparison.Ordinal));
}

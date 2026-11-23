using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using System.Text;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.FakeProvider;

/// <summary>An in-memory <see cref="ICloudProvider"/> for tests and for running the daemon without OneDrive. Serves scripted or generated trees and records every upload, rename and delete.</summary>
/// <param name="pageSize">How many items a snapshot or delta page holds.</param>
internal sealed class FakeCloudProvider(int pageSize) : ICloudProvider
{
    private const string GeneratedIdPrefix = "item-";

    public static readonly RemoteItemId DefaultRoot = new("root");

    private readonly Dictionary<RemoteItemId, ItemSource> sources = [];
    private readonly Dictionary<RemoteItemId, ProviderError> failingRoots = [];
    private readonly Queue<ProviderError> scriptedFailures = new();
    private readonly Dictionary<RemoteItemId, List<ProviderItem>> children = [];
    private readonly Dictionary<RemoteItemId, List<ProviderChange>> changes = [];
    private readonly HashSet<DeltaToken> expiredTokens = [];
    private readonly Dictionary<RemoteItemId, int> listingsCompleted = [];
    private int tokensIssued;

    public Action<int>? BeforePage { get; set; }

    public ProviderDriveInfo DriveInfo { get; set; } = new(0, 0, 0, Option.None<long>());

    public bool ServeGeneratedContent { get; set; }

    public Dictionary<RemoteItemId, byte[]> Contents { get; } = [];

    public HashSet<RemoteItemId> TransientDownloads { get; } = [];

    public List<(RelativePath Path, byte[] Content, DateTimeOffset ModifiedUtc)> Uploads { get; } = [];

    public List<(RemoteItemId ItemId, RelativePath Path)> Renames { get; } = [];

    public List<RemoteItemId> Deletes { get; } = [];

    public int PagesServed { get; private set; }

    public int ItemsServed { get; private set; }

    public List<string?> ResumeTokensSeen { get; } = [];

    public void AddItem(ProviderItem item, RemoteItemId? root = null)
    {
        var key = root ?? DefaultRoot;
        if (!sources.TryGetValue(key, out var source)) sources[key] = source = ItemSource.FromList([]);

        source.Add(item);
    }

    public void AddChild(RemoteItemId parent, ProviderItem item)
    {
        if (!children.TryGetValue(parent, out var list)) children[parent] = list = [];
        list.Add(item);
    }

    public void GenerateTree(int count, RemoteItemId? root = null)
        => sources[root ?? DefaultRoot] = new ItemSource(count, index =>
        {
            var isFolder = index % 10 == 0;
            var name = isFolder ? $"folder-{index}" : $"file-{index}.txt";

            return new ProviderItem(new RemoteItemId($"{GeneratedIdPrefix}{index}"), new RelativePath(name), isFolder ? ItemType.Folder : ItemType.File, isFolder ? 0 : index, DateTimeOffset.UnixEpoch.AddSeconds(index));
        });

    public void ScriptChanges(RemoteItemId root, params ProviderChange[] scripted)
    {
        if (!changes.TryGetValue(root, out var list)) changes[root] = list = [];
        list.AddRange(scripted);
    }

    public void ExpireToken(DeltaToken token) => expiredTokens.Add(token);

    public void FailRoot(RemoteItemId root, ProviderError failure) => failingRoots[root] = failure;

    public void ScriptFailures(params ProviderError[] failures)
    {
        foreach (var failure in failures)
            scriptedFailures.Enqueue(failure);
    }

    public Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Result<ProviderAccount, ProviderError> result = scriptedFailures.TryDequeue(out var failure)
            ? new Fail<ProviderError>(failure)
            : new Ok<ProviderAccount>(new ProviderAccount("fake-account", "Fake Account"));

        return Task.FromResult(result);
    }

    public Task<Result<ProviderAccount, ProviderError>> ReconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<ProviderAccount, ProviderError>>(new Ok<ProviderAccount>(new ProviderAccount(accountIdentifier, "Fake Account")));
    }

    public Task<Result<Unit, ProviderError>> DisconnectAsync(string accountIdentifier, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    public Task<Result<ProviderDriveInfo, ProviderError>> GetDriveInfoAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Result<ProviderDriveInfo, ProviderError> result = scriptedFailures.TryDequeue(out var failure)
            ? new Fail<ProviderError>(failure)
            : new Ok<ProviderDriveInfo>(DriveInfo);

        return Task.FromResult(result);
    }

    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(RemoteItemId parent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Result<IReadOnlyList<ProviderItem>, ProviderError> result = scriptedFailures.TryDequeue(out var failure)
            ? new Fail<ProviderError>(failure)
            : new Ok<IReadOnlyList<ProviderItem>>(children.TryGetValue(parent, out var list) ? list.ToList() : []);

        return Task.FromResult(result);
    }

    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
    {
        BeforePage?.Invoke(PagesServed + 1);
        cancellationToken.ThrowIfCancellationRequested();

        if (failingRoots.TryGetValue(root, out var rootFailure)) return Task.FromResult<Result<ProviderSnapshotPage, ProviderError>>(new Fail<ProviderError>(rootFailure));

        if (scriptedFailures.TryDequeue(out var failure)) return Task.FromResult<Result<ProviderSnapshotPage, ProviderError>>(new Fail<ProviderError>(failure));

        var source = sources.GetValueOrDefault(root) ?? ItemSource.FromList([]);
        var offset = resumeToken is null ? 0 : int.Parse(resumeToken);
        var page = Enumerable.Range(offset, Math.Min(pageSize, Math.Max(0, source.Count - offset))).Select(source.At).ToList();
        var next = offset + page.Count;
        var last = next >= source.Count;
        PagesServed++;
        ItemsServed += page.Count;
        ResumeTokensSeen.Add(resumeToken);
        var token = last ? new DeltaToken(NextListingToken(root)) : (DeltaToken?)null;

        return Task.FromResult<Result<ProviderSnapshotPage, ProviderError>>(new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage(page, last ? null : next.ToString(), token, source.Count)));
    }

    public Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (scriptedFailures.TryDequeue(out var failure)) return Task.FromResult<Result<ProviderDeltaPage, ProviderError>>(new Fail<ProviderError>(failure));

        if (expiredTokens.Contains(token)) return Task.FromResult<Result<ProviderDeltaPage, ProviderError>>(new Fail<ProviderError>(new InvalidDeltaToken($"token {token.Value} expired")));

        var scripted = changes.GetValueOrDefault(root) ?? [];
        var offset = resumeToken is null ? 0 : int.Parse(resumeToken);
        var page = scripted.Skip(offset).Take(pageSize).ToList();
        var next = offset + page.Count;
        var last = next >= scripted.Count;
        var newToken = last ? new DeltaToken($"delta-{root.Value}-{++tokensIssued}") : (DeltaToken?)null;

        return Task.FromResult<Result<ProviderDeltaPage, ProviderError>>(new Ok<ProviderDeltaPage>(new ProviderDeltaPage(page, last ? null : next.ToString(), newToken)));
    }

    public Task<Result<Stream, ProviderError>> DownloadAsync(RemoteItemId itemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Result<Stream, ProviderError> result = TransientDownloads.Contains(itemId)
            ? new Fail<ProviderError>(new Transient($"Try again for {itemId.Value}"))
            : Contents.TryGetValue(itemId, out var bytes)
                ? new Ok<Stream>(new MemoryStream(bytes))
                : ServeGeneratedContent && itemId.Value.StartsWith(GeneratedIdPrefix, StringComparison.Ordinal)
                    ? new Ok<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(itemId.Value)))
                    : new Fail<ProviderError>(new Permanent($"No content for {itemId.Value}"));

        return Task.FromResult(result);
    }

    public async Task<Result<Unit, ProviderError>> UploadAsync(UploadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var copy = new MemoryStream();
        await request.Content.CopyToAsync(copy, cancellationToken);
        Uploads.Add((request.Path, copy.ToArray(), request.ModifiedUtc));

        return new Ok<Unit>(default);
    }

    public Task<Result<Unit, ProviderError>> RenameAsync(RemoteItemId itemId, RelativePath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Renames.Add((itemId, path));

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    public Task<Result<Unit, ProviderError>> DeleteAsync(RemoteItemId itemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Deletes.Add(itemId);

        return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }

    private string NextListingToken(RemoteItemId root)
    {
        var count = listingsCompleted.GetValueOrDefault(root);
        listingsCompleted[root] = count + 1;

        return count == 0 ? $"delta-{root.Value}" : $"delta-{root.Value}-relisted-{count}";
    }

    private sealed class ItemSource(int count, Func<int, ProviderItem> at)
    {
        private readonly List<ProviderItem>? list;

        private ItemSource(List<ProviderItem> list)
            : this(0, index => list[index]) => this.list = list;

        public int Count => list?.Count ?? count;

        public Func<int, ProviderItem> At => at;

        public static ItemSource FromList(List<ProviderItem> list) => new(list);

        public void Add(ProviderItem item) => list!.Add(item);
    }
}

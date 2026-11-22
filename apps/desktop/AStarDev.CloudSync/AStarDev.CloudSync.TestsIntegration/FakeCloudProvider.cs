using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class FakeCloudProvider(int pageSize) : ICloudProvider
{
    public static readonly RemoteItemId DefaultRoot = new("root");

    private readonly Dictionary<RemoteItemId, ItemSource> sources = [];
    private readonly Dictionary<RemoteItemId, ProviderError> failingRoots = [];
    private readonly Queue<ProviderError> scriptedFailures = new();
    private readonly Dictionary<RemoteItemId, List<ProviderItem>> children = [];
    private readonly Dictionary<RemoteItemId, List<ProviderChange>> changes = [];
    private readonly HashSet<DeltaToken> expiredTokens = [];
    private int tokensIssued;

    public Action<int>? BeforePage { get; set; }

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

            return new ProviderItem(new RemoteItemId($"item-{index}"), new RelativePath(name), isFolder ? ItemType.Folder : ItemType.File, isFolder ? 0 : index, DateTimeOffset.UnixEpoch.AddSeconds(index));
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

        return Task.FromResult<Result<ProviderSnapshotPage, ProviderError>>(new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage(page, last ? null : next.ToString(), last ? new DeltaToken($"delta-{root.Value}") : null, source.Count)));
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

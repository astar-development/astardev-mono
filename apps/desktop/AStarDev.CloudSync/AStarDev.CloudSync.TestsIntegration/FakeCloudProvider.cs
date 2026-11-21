using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class FakeCloudProvider(int pageSize) : ICloudProvider
{
    private readonly List<ProviderItem> items = [];
    private readonly Queue<ProviderError> scriptedFailures = new();
    private int cursor;

    public void AddItem(ProviderItem item) => items.Add(item);

    public void GenerateTree(int count)
    {
        for (var index = 0; index < count; index++)
        {
            var isFolder = index % 10 == 0;
            var name = isFolder ? $"folder-{index}" : $"file-{index}.txt";
            items.Add(new ProviderItem(new RemoteItemId($"item-{index}"), new RelativePath(name), isFolder ? ItemType.Folder : ItemType.File, isFolder ? 0 : index, DateTimeOffset.UnixEpoch.AddSeconds(index)));
        }
    }

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

    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (scriptedFailures.TryDequeue(out var failure))
        {
            return Task.FromResult<Result<ProviderSnapshotPage, ProviderError>>(new Fail<ProviderError>(failure));
        }

        var page = items.Skip(cursor).Take(pageSize).ToList();
        cursor += page.Count;

        return Task.FromResult<Result<ProviderSnapshotPage, ProviderError>>(new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage(page)));
    }
}

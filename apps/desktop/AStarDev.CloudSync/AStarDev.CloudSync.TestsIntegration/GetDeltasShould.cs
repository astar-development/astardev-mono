using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class GetDeltasShould
{
    private static readonly RemoteItemId Scope = new("root");
    private static readonly DeltaToken Token = new("delta-root");

    [Fact]
    public async Task page_changes_and_return_a_new_token()
    {
        var provider = new FakeCloudProvider(pageSize: 2);
        var item = new ProviderItem(new RemoteItemId("a"), new RelativePath("b.txt"), ItemType.File, 1, DateTimeOffset.UnixEpoch);
        provider.ScriptChanges(
            Scope,
            new ProviderChange(ChangeKind.Created, item.RemoteItemId, item),
            new ProviderChange(ChangeKind.Renamed, item.RemoteItemId, item, new RelativePath("a.txt")),
            new ProviderChange(ChangeKind.Deleted, item.RemoteItemId));
        var kinds = new List<ChangeKind>();
        var pageSizes = new List<int>();
        string? resumeToken = null;
        ProviderDeltaPage last;

        do
        {
            var result = await provider.GetDeltasAsync("account", Scope, Token, resumeToken, TestContext.Current.CancellationToken);
            last = result.Value.ShouldBeOfType<Ok<ProviderDeltaPage>>().Value;
            pageSizes.Add(last.Changes.Count);
            kinds.AddRange(last.Changes.Select(change => change.Kind));
            resumeToken = last.ResumeToken;
        }
        while (resumeToken is not null);

        pageSizes.ShouldBe([2, 1]);
        kinds.ShouldBe([ChangeKind.Created, ChangeKind.Renamed, ChangeKind.Deleted]);
        last.DeltaToken.ShouldNotBeNull().ShouldNotBe(Token);
    }

    [Fact]
    public async Task report_an_invalid_token()
    {
        var provider = new FakeCloudProvider(pageSize: 2);
        provider.ExpireToken(Token);

        var result = await provider.GetDeltasAsync("account", Scope, Token, null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<InvalidDeltaToken>();
    }

    [Fact]
    public async Task throw_when_cancelled()
    {
        var provider = new FakeCloudProvider(pageSize: 2);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetDeltasAsync("account", Scope, Token, null, cancellation.Token));
    }
}

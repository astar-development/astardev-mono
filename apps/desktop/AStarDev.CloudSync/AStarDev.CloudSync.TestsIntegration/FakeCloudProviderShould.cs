using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class FakeCloudProviderShould
{
    private static readonly RemoteItemId Scope = new("root");

    [Fact]
    public async Task page_through_a_generated_tree_of_the_requested_size()
    {
        var provider = new FakeCloudProvider(pageSize: 40);
        provider.GenerateTree(100);
        var seen = new List<ProviderItem>();
        var pageSizes = new List<int>();
        string? resumeToken = null;

        do
        {
            var page = await provider.GetSnapshotAsync(Scope, resumeToken, TestContext.Current.CancellationToken);
            var served = page.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value;
            pageSizes.Add(served.Items.Count);
            seen.AddRange(served.Items);
            resumeToken = served.ResumeToken;
        }
        while (resumeToken is not null);

        pageSizes.ShouldBe([40, 40, 20]);
        seen.Select(item => item.RemoteItemId).Distinct().Count().ShouldBe(100);
    }

    [Fact]
    public async Task list_only_the_children_of_the_requested_parent()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        var parent = new RemoteItemId("docs");
        var child = new ProviderItem(new RemoteItemId("reports"), new RelativePath("docs/Reports"), ItemType.Folder, 0, DateTimeOffset.UnixEpoch);
        provider.AddChild(parent, child);
        provider.AddChild(new RemoteItemId("other"), child with { RemoteItemId = new RemoteItemId("elsewhere") });

        var listed = await provider.ListChildrenAsync(parent, TestContext.Current.CancellationToken);
        var empty = await provider.ListChildrenAsync(new RemoteItemId("unknown"), TestContext.Current.CancellationToken);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<ProviderItem>>>().Value.ShouldBe([child]);
        empty.Value.ShouldBeOfType<Ok<IReadOnlyList<ProviderItem>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task fail_as_scripted_then_succeed()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        provider.GenerateTree(3);
        provider.ScriptFailures(new Transient("network drop"), new RateLimited(TimeSpan.FromSeconds(7)), new AuthenticationFailed("expired"));

        var first = await provider.GetSnapshotAsync(Scope, null, TestContext.Current.CancellationToken);
        var second = await provider.GetSnapshotAsync(Scope, null, TestContext.Current.CancellationToken);
        var third = await provider.GetSnapshotAsync(Scope, null, TestContext.Current.CancellationToken);
        var fourth = await provider.GetSnapshotAsync(Scope, null, TestContext.Current.CancellationToken);

        first.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
        second.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<RateLimited>().RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
        third.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
        fourth.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task connect_to_a_fake_account()
    {
        var provider = new FakeCloudProvider(pageSize: 10);

        var result = await provider.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<ProviderAccount>>().Value.AccountIdentifier.ShouldBe("fake-account");
    }

    [Fact]
    public async Task fail_connect_as_scripted()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        provider.ScriptFailures(new AuthenticationFailed("refused"));

        var result = await provider.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task throw_when_the_snapshot_is_cancelled()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetSnapshotAsync(Scope, null, cancellation.Token));
    }

    [Fact]
    public async Task throw_when_the_connect_is_cancelled()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.ConnectAsync(cancellation.Token));
    }

    [Fact]
    public async Task report_the_configured_drive_info()
    {
        var provider = new FakeCloudProvider(pageSize: 10) { DriveInfo = new ProviderDriveInfo(30, 70, 100, Option.Some(1234L)) };

        var result = await provider.GetDriveInfoAsync(TestContext.Current.CancellationToken);

        var info = result.Value.ShouldBeOfType<Ok<ProviderDriveInfo>>().Value;
        (info.UsedBytes, info.RemainingBytes, info.TotalBytes).ShouldBe((30L, 70L, 100L));
        info.ItemCountEstimate.ShouldBe(Option.Some(1234L));
    }

    [Fact]
    public async Task fail_the_drive_info_with_a_scripted_failure()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        provider.ScriptFailures(new Transient("down"));

        var result = await provider.GetDriveInfoAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task throw_when_the_drive_info_is_cancelled()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetDriveInfoAsync(cancellation.Token));
    }
}

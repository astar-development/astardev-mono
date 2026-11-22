using System.Net;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class PathResolutionShould
{
    private static readonly RemoteItemId DriveRoot = new("root");

    [Fact]
    public async Task build_paths_from_parent_ids_when_graph_omits_the_path()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page(Top("R"), Folder("d", "docs", "R"), File("f", "a.txt", "d"), File("g", "b.txt", "R"))));

        var page = await Snapshot(handler, DriveRoot);

        page.Items.Select(item => (item.RemoteItemId.Value, item.Path.Value)).ShouldBe([("d", "docs"), ("f", "docs/a.txt"), ("g", "b.txt")]);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task resolve_a_child_listed_before_its_parent()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page(File("f", "a.txt", "inner"), Folder("inner", "inner", "d"), Folder("d", "docs", "R"), Top("R"))));

        var page = await Snapshot(handler, DriveRoot);

        page.Items.Select(item => (item.RemoteItemId.Value, item.Path.Value)).ShouldBe([("f", "docs/inner/a.txt"), ("inner", "docs/inner"), ("d", "docs")]);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task make_paths_relative_to_a_subfolder_root()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page(Top("R"), Folder("d", "docs", "R"), Folder("s", "sub", "d"), File("f", "a.txt", "s"), Folder("i", "inner", "s"), File("g", "b.txt", "i"), File("h", "other.txt", "d"))));

        var page = await Snapshot(handler, new RemoteItemId("s"));

        page.Items.Select(item => (item.RemoteItemId.Value, item.Path.Value)).ShouldBe([("f", "a.txt"), ("i", "inner"), ("g", "inner/b.txt")]);
    }

    [Fact]
    public async Task make_delta_paths_relative_to_a_subfolder_root_with_the_same_mapping()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page(Top("R"), Folder("d", "docs", "R"), Folder("s", "sub", "d"), File("f", "a.txt", "s"))));
        using var telemetry = new OneDriveTelemetry();

        var result = await new RootEnumerator(Client(handler), telemetry).GetDeltasAsync(new RemoteItemId("s"), new DeltaToken("https://graph.test/delta?token=t"), null, TestContext.Current.CancellationToken);

        var page = result.Value.ShouldBeOfType<Ok<ProviderDeltaPage>>().Value;
        page.Changes.Select(change => (change.RemoteItemId.Value, change.Item!.Path.Value)).ShouldBe([("f", "a.txt")]);
    }

    [Fact]
    public async Task resume_with_the_map_rebuilt()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsolutePath.EndsWith("items/d")
            ? FakeGraphHandler.Json("""{ "id": "d", "name": "docs", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {}, "parentReference": { "id": "R" } }""")
            : request.RequestUri.AbsolutePath.EndsWith("items/R")
                ? FakeGraphHandler.Json("""{ "id": "R", "name": "root", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {} }""")
                : FakeGraphHandler.Json(Page(File("f", "a.txt", "d"), File("g", "b.txt", "d"))));

        var page = await Snapshot(handler, DriveRoot, "https://graph.test/next?page=2");

        page.Items.Select(item => item.Path.Value).ShouldBe(["docs/a.txt", "docs/b.txt"]);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task reuse_known_folders_across_pages_of_one_enumeration()
    {
        var pages = new Queue<string>([Page(Top("R"), Folder("d", "docs", "R")), Page(File("f", "a.txt", "d"))]);
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(pages.Dequeue()));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        await enumerator.GetSnapshotAsync(DriveRoot, null, TestContext.Current.CancellationToken);
        var second = await enumerator.GetSnapshotAsync(DriveRoot, "https://graph.test/next", TestContext.Current.CancellationToken);

        second.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value.Items.Single().Path.Value.ShouldBe("docs/a.txt");
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task follow_a_renamed_folder_for_children_of_folders_it_did_not_list()
    {
        var pages = new Queue<string>([Page(Top("R"), Folder("d", "docs", "R"), Folder("s", "sub", "d")), Page(Folder("d", "renamed", "R"), File("f", "a.txt", "s"))]);
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsolutePath.EndsWith("items/s") ? FakeGraphHandler.Json(Folder("s", "sub", "d")) : request.RequestUri.AbsolutePath.EndsWith("items/R") ? FakeGraphHandler.Json(Top("R")) : FakeGraphHandler.Json(pages.Dequeue()));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        await enumerator.GetSnapshotAsync(DriveRoot, null, TestContext.Current.CancellationToken);
        var second = await enumerator.GetSnapshotAsync(DriveRoot, "https://graph.test/next", TestContext.Current.CancellationToken);

        var items = second.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value.Items;
        items.Select(item => item.Path.Value).ShouldBe(["renamed", "renamed/sub/a.txt"]);
    }

    [Fact]
    public async Task leave_out_an_item_whose_parent_no_longer_exists()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsolutePath.EndsWith("items/d")
            ? FakeGraphHandler.Json("{}", HttpStatusCode.NotFound)
            : FakeGraphHandler.Json(Page(File("f", "a.txt", "d"), File("g", "b.txt", "R"), Top("R"))));

        var page = await Snapshot(handler, DriveRoot);

        page.Items.Select(item => item.Path.Value).ShouldBe(["b.txt"]);
    }

    [Fact]
    public async Task return_the_failure_when_a_parent_cannot_be_fetched()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsolutePath.EndsWith("items/d")
            ? FakeGraphHandler.Json("{}", HttpStatusCode.ServiceUnavailable)
            : FakeGraphHandler.Json(Page(File("f", "a.txt", "d"))));
        using var telemetry = new OneDriveTelemetry();

        var result = await new RootEnumerator(Client(handler), telemetry).GetSnapshotAsync(DriveRoot, null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task honour_cancellation_while_fetching_a_parent()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}"));
        using var telemetry = new OneDriveTelemetry();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var item = new GraphDriveItem("f", "a.txt", 1, DateTimeOffset.UnixEpoch, null, new GraphParentReference(null, "d"));

        await Should.ThrowAsync<OperationCanceledException>(() => new PathResolver(Client(handler), telemetry).ResolveAsync(DriveRoot, [item], cancelled.Token));
    }

    private static async Task<ProviderSnapshotPage> Snapshot(FakeGraphHandler handler, RemoteItemId root, string? resumeToken = null)
    {
        using var telemetry = new OneDriveTelemetry();
        var result = await new RootEnumerator(Client(handler), telemetry).GetSnapshotAsync(root, resumeToken, TestContext.Current.CancellationToken);

        return result.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value;
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };

    private static string Page(params string[] items) => $$"""{ "value": [ {{string.Join(',', items)}} ], "@odata.deltaLink": "https://graph.test/delta?token=t" }""";

    private static string Top(string id) => $$"""{ "id": "{{id}}", "name": "root", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {} }""";

    private static string Folder(string id, string name, string parentId) => $$"""{ "id": "{{id}}", "name": "{{name}}", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {}, "parentReference": { "id": "{{parentId}}" } }""";

    private static string File(string id, string name, string parentId) => $$"""{ "id": "{{id}}", "name": "{{name}}", "size": 1, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "file": {}, "parentReference": { "id": "{{parentId}}" } }""";
}

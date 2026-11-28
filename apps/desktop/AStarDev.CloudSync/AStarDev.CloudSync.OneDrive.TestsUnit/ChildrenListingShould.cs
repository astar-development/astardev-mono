using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class ChildrenListingShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_folders_for_a_parent()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""
            { "value": [
              { "id": "f1", "name": "Documents", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": { "childCount": 2 }, "parentReference": { "path": "/drive/root:" } },
              { "id": "a1", "name": "notes.txt", "size": 4, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:" } } ] }
            """));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.ListChildrenAsync(new RemoteItemId("parent 1"), Token);

        var items = result.Value.ShouldBeOfType<Ok<IReadOnlyList<ProviderItem>>>().Value;
        items.Select(item => (item.RemoteItemId.Value, item.ItemType)).ShouldBe([("f1", ItemType.Folder), ("a1", ItemType.File)]);
        handler.Requests.Single().AbsolutePath.ShouldEndWith("me/drive/items/parent%201/children");
    }

    [Fact]
    public async Task page_through_a_large_folder()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsoluteUri switch
        {
            var uri when uri.Contains("page=3") => FakeGraphHandler.Json(Page("c", null)),
            var uri when uri.Contains("page=2") => FakeGraphHandler.Json(Page("b", "https://graph.test/next?page=3")),
            _ => FakeGraphHandler.Json(Page("a", "https://graph.test/next?page=2"))
        });
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.ListChildrenAsync(new RemoteItemId("root"), Token);

        result.Value.ShouldBeOfType<Ok<IReadOnlyList<ProviderItem>>>().Value.Select(item => item.RemoteItemId.Value).ShouldBe(["a", "b", "c"]);
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task request_a_thousand_children_per_page_and_follow_the_next_link_as_given()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsoluteUri.Contains("page=2")
            ? FakeGraphHandler.Json(Page("b", null))
            : FakeGraphHandler.Json(Page("a", "https://graph.test/next?$top=1000&page=2")));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        await enumerator.ListChildrenAsync(new RemoteItemId("root"), Token);

        handler.Requests.Select(uri => uri.AbsoluteUri).ShouldBe(["https://graph.test/v1.0/me/drive/items/root/children?$top=1000", "https://graph.test/next?$top=1000&page=2"]);
    }

    [Fact]
    public async Task map_a_graph_failure_to_a_provider_error()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", System.Net.HttpStatusCode.Unauthorized));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.ListChildrenAsync(new RemoteItemId("root"), Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page("a", null)));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => enumerator.ListChildrenAsync(new RemoteItemId("root"), cancelled.Token));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };

    private static string Page(string id, string? nextLink)
    {
        var next = nextLink is null ? string.Empty : $", \"@odata.nextLink\": \"{nextLink}\"";

        return $$"""{ "value": [ { "id": "{{id}}", "name": "{{id}}", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {}, "parentReference": { "path": "/drive/root:" } } ]{{next}} }""";
    }
}

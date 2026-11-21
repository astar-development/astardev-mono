using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class RootEnumerationShould
{
    private static readonly SyncScope Root = new(new RemoteItemId("root"));

    [Fact]
    public async Task follow_next_links_until_the_last_page()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsoluteUri switch
        {
            var uri when uri.Contains("page=3") => FakeGraphHandler.Json(Page("c", null)),
            var uri when uri.Contains("page=2") => FakeGraphHandler.Json(Page("b", "https://graph.test/next?page=3")),
            _ => FakeGraphHandler.Json(Page("a", "https://graph.test/next?page=2"))
        });
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.GetSnapshotAsync(Root, TestContext.Current.CancellationToken);

        var page = result.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value;
        page.Items.Select(item => item.RemoteItemId.Value).ShouldBe(["a", "b", "c"]);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests[0].AbsolutePath.ShouldEndWith("me/drive/items/root/children");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page("a", null)));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => enumerator.GetSnapshotAsync(Root, cancelled.Token));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };

    private static string Page(string id, string? nextLink)
    {
        var next = nextLink is null ? string.Empty : $", \"@odata.nextLink\": \"{nextLink}\"";

        return $$"""{ "value": [ { "id": "{{id}}", "name": "{{id}}.txt", "size": 1, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:" } } ]{{next}} }""";
    }
}

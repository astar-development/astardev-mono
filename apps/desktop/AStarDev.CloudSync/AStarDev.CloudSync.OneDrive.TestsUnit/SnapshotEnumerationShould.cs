using System.Net;
using System.Net.Http.Headers;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class SnapshotEnumerationShould
{
    private static readonly RemoteItemId Root = new("root");

    [Fact]
    public async Task walk_nested_folders()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""
            { "value": [
              { "id": "root", "name": "root", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {} },
              { "id": "d", "name": "docs", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {}, "parentReference": { "path": "/drive/root:" } },
              { "id": "f", "name": "deep.txt", "size": 7, "lastModifiedDateTime": "2026-01-02T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:/docs/sub" } },
              { "id": "x", "name": "gone.txt", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "deleted": {} }
            ] }
            """));

        var page = await Fetch(handler, null);

        page.Items.Select(item => item.RemoteItemId.Value).ShouldBe(["d", "f"]);
        page.Items[0].ItemType.ShouldBe(ItemType.Folder);
        page.Items[1].Path.ShouldBe(new RelativePath("docs/sub/deep.txt"));
        page.Items[1].Size.ShouldBe(7);
        page.Items[1].ModifiedUtc.ShouldBe(DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        handler.Requests[0].AbsolutePath.ShouldEndWith("me/drive/items/root/delta");
    }

    [Fact]
    public async Task page_through_a_large_folder()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsoluteUri.Contains("page=2")
            ? FakeGraphHandler.Json(Page("b", null, "https://graph.test/delta?token=t"))
            : FakeGraphHandler.Json(Page("a", "https://graph.test/next?page=2", null)));

        var first = await Fetch(handler, null);
        var second = await Fetch(handler, first.ResumeToken);

        first.Items.Select(item => item.RemoteItemId.Value).ShouldBe(["a"]);
        first.ResumeToken.ShouldBe("https://graph.test/next?page=2");
        first.DeltaToken.ShouldBeNull();
        second.Items.Select(item => item.RemoteItemId.Value).ShouldBe(["b"]);
    }

    [Fact]
    public async Task resume_from_a_token()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page("b", null, "https://graph.test/delta?token=t")));

        await Fetch(handler, "https://graph.test/next?page=2");

        handler.Requests.ShouldHaveSingleItem().AbsoluteUri.ShouldBe("https://graph.test/next?page=2");
    }

    [Fact]
    public async Task return_the_initial_delta_token_on_the_last_page()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page("a", null, "https://graph.test/delta?token=t")));

        var page = await Fetch(handler, null);

        page.ResumeToken.ShouldBeNull();
        page.DeltaToken.ShouldBe(new DeltaToken("https://graph.test/delta?token=t"));
    }

    [Fact]
    public async Task map_throttling_to_a_provider_error()
    {
        var handler = new FakeGraphHandler(_ =>
        {
            var response = FakeGraphHandler.Json("{}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(12));

            return response;
        });
        using var telemetry = new OneDriveTelemetry();

        var result = await new RootEnumerator(Client(handler), telemetry).GetSnapshotAsync(Root, null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<RateLimited>().RetryAfter.ShouldBe(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public async Task map_a_server_error_to_a_transient_error()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        using var telemetry = new OneDriveTelemetry();

        var result = await new RootEnumerator(Client(handler), telemetry).GetSnapshotAsync(Root, null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json(Page("a", null, null)));
        using var telemetry = new OneDriveTelemetry();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new RootEnumerator(Client(handler), telemetry).GetSnapshotAsync(Root, null, cancelled.Token));
    }

    private static async Task<ProviderSnapshotPage> Fetch(FakeGraphHandler handler, string? resumeToken)
    {
        using var telemetry = new OneDriveTelemetry();
        var result = await new RootEnumerator(Client(handler), telemetry).GetSnapshotAsync(Root, resumeToken, TestContext.Current.CancellationToken);

        return result.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value;
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };

    private static string Page(string id, string? nextLink, string? deltaLink)
    {
        var next = nextLink is null ? string.Empty : $", \"@odata.nextLink\": \"{nextLink}\"";
        var delta = deltaLink is null ? string.Empty : $", \"@odata.deltaLink\": \"{deltaLink}\"";

        return $$"""{ "value": [ { "id": "{{id}}", "name": "{{id}}.txt", "size": 1, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:" } } ]{{next}}{{delta}} }""";
    }
}

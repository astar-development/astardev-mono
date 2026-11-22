using System.Net;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class DeltaMappingShould
{
    private static readonly RemoteItemId Root = new("root");

    private const string Items = """
        { "id": "new", "name": "new.txt", "size": 1, "createdDateTime": "2026-01-01T00:00:00Z", "lastModifiedDateTime": "2026-01-01T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:/Docs" } },
        { "id": "old", "name": "old.txt", "size": 2, "createdDateTime": "2025-01-01T00:00:00Z", "lastModifiedDateTime": "2026-01-02T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:" } },
        { "id": "gone", "deleted": { "state": "deleted" } },
        { "id": "moved", "name": "moved.txt", "size": 3, "createdDateTime": "2025-01-01T00:00:00Z", "lastModifiedDateTime": "2026-01-03T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:/Archive" } }
        """;

    [Fact]
    public async Task map_created_modified_deleted_and_renamed_items()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json($$"""{ "value": [ {{Items}} ], "@odata.deltaLink": "https://graph.test/delta?token=next" }"""));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.GetDeltasAsync(Root, new DeltaToken("https://graph.test/delta?token=old"), null, TestContext.Current.CancellationToken);

        var page = result.Value.ShouldBeOfType<Ok<ProviderDeltaPage>>().Value;
        page.Changes.Select(change => (change.Kind, change.RemoteItemId.Value)).ShouldBe(
        [
            (ChangeKind.Created, "new"),
            (ChangeKind.Modified, "old"),
            (ChangeKind.Deleted, "gone"),
            (ChangeKind.Modified, "moved")
        ]);
        page.Changes[0].Item!.Path.Value.ShouldBe("Docs/new.txt");
        page.Changes[2].Item.ShouldBeNull();
        page.Changes[3].Item!.Path.Value.ShouldBe("Archive/moved.txt");
        page.ResumeToken.ShouldBeNull();
        page.DeltaToken.ShouldBe(new DeltaToken("https://graph.test/delta?token=next"));
        handler.Requests.Single().AbsoluteUri.ShouldBe("https://graph.test/delta?token=old");
    }

    [Fact]
    public async Task continue_from_the_resume_token_and_hold_back_the_delta_token_until_the_last_page()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "value": [], "@odata.nextLink": "https://graph.test/delta?page=2" }"""));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.GetDeltasAsync(Root, new DeltaToken("https://graph.test/delta?token=old"), "https://graph.test/delta?page=1", TestContext.Current.CancellationToken);

        var page = result.Value.ShouldBeOfType<Ok<ProviderDeltaPage>>().Value;
        page.ResumeToken.ShouldBe("https://graph.test/delta?page=2");
        page.DeltaToken.ShouldBeNull();
        handler.Requests.Single().AbsoluteUri.ShouldBe("https://graph.test/delta?page=1");
    }

    [Fact]
    public async Task map_gone_to_an_invalid_token()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "error": { "code": "resyncRequired" } }""", HttpStatusCode.Gone));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.GetDeltasAsync(Root, new DeltaToken("https://graph.test/delta?token=old"), null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<InvalidDeltaToken>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "value": [] }"""));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => enumerator.GetDeltasAsync(Root, new DeltaToken("t"), null, cancelled.Token));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };
}

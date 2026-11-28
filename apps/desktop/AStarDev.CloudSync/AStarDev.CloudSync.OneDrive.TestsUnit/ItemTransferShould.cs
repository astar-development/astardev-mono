using System.Net;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class ItemTransferShould
{
    private static readonly DateTimeOffset Modified = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HttpClient Client(FakeGraphHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };

    [Fact]
    public async Task download_the_content_of_an_item()
    {
        var handler = new FakeGraphHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("hello") });
        using var client = Client(handler);

        var result = await new ItemTransfer(client, client).DownloadAsync(new RemoteItemId("a 1"), Token);

        await using var stream = result.Value.ShouldBeOfType<Ok<Stream>>().Value;
        (await new StreamReader(stream).ReadToEndAsync(Token)).ShouldBe("hello");
        handler.Calls.Single().Uri.AbsolutePath.ShouldEndWith("me/drive/items/a%201/content");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, typeof(Permanent))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AuthenticationFailed))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(RateLimited))]
    [InlineData(HttpStatusCode.BadGateway, typeof(Transient))]
    public async Task map_a_failed_download_to_a_provider_error(HttpStatusCode status, Type expected)
    {
        using var client = Client(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", status)));

        var result = await new ItemTransfer(client, client).DownloadAsync(new RemoteItemId("a"), Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType(expected);
    }

    [Fact]
    public async Task map_a_dropped_connection_to_a_transient_error()
    {
        using var client = Client(new FakeGraphHandler(_ => throw new HttpRequestException("dropped")));

        var result = await new ItemTransfer(client, client).DownloadAsync(new RemoteItemId("a"), Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task upload_a_small_file_in_one_request_and_set_its_modified_time()
    {
        var handler = new FakeGraphHandler(request => request.Method == HttpMethod.Put
            ? FakeGraphHandler.Json("""{ "id": "new-1", "name": "b.txt", "size": 3, "lastModifiedDateTime": "2026-01-01T00:00:00Z" }""", HttpStatusCode.Created)
            : FakeGraphHandler.Json("{}"));
        using var client = Client(handler);
        await using var content = new MemoryStream("abc"u8.ToArray());

        var result = await new ItemTransfer(client, client).UploadAsync(new UploadRequest(new RelativePath("Docs/b c.txt"), content, Modified), Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        handler.Calls[0].Method.ShouldBe(HttpMethod.Put);
        handler.Calls[0].Uri.AbsolutePath.ShouldEndWith("me/drive/root:/Docs/b%20c.txt:/content");
        handler.Calls[0].Body.ShouldBe("abc");
        handler.Calls[1].Method.ShouldBe(HttpMethod.Patch);
        handler.Calls[1].Uri.AbsolutePath.ShouldEndWith("me/drive/items/new-1");
        handler.Calls[1].Body.ShouldContain("2026-03-04T05:06:07");
    }

    [Fact]
    public async Task upload_a_large_file_in_chunks_of_one_upload_session()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.Host switch
        {
            "upload.test" => FakeGraphHandler.Json("{}", HttpStatusCode.Accepted),
            _ => FakeGraphHandler.Json("""{ "uploadUrl": "https://upload.test/session-1" }""")
        });
        using var graph = Client(handler);
        using var uploads = Client(handler);
        var size = ItemTransfer.SimpleUploadLimit + 1;
        await using var content = new MemoryStream(new byte[size]);

        var result = await new ItemTransfer(graph, uploads, chunkSize: 3 * 1024 * 1024).UploadAsync(new UploadRequest(new RelativePath("big.bin"), content, Modified), Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        handler.Calls[0].Method.ShouldBe(HttpMethod.Post);
        handler.Calls[0].Uri.AbsolutePath.ShouldEndWith("me/drive/root:/big.bin:/createUploadSession");
        handler.Calls[0].Body.ShouldContain("2026-03-04T05:06:07");
        handler.Calls.Skip(1).Select(call => call.ContentRange).ShouldBe(["bytes 0-3145727/4194305", "bytes 3145728-4194304/4194305"]);
    }

    [Fact]
    public async Task map_a_failed_chunk_to_a_provider_error()
    {
        var handler = new FakeGraphHandler(request => request.RequestUri!.Host == "upload.test"
            ? FakeGraphHandler.Json("{}", HttpStatusCode.ServiceUnavailable)
            : FakeGraphHandler.Json("""{ "uploadUrl": "https://upload.test/session-1" }"""));
        using var client = Client(handler);
        await using var content = new MemoryStream(new byte[ItemTransfer.SimpleUploadLimit + 1]);

        var result = await new ItemTransfer(client, client).UploadAsync(new UploadRequest(new RelativePath("big.bin"), content, Modified), Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task rename_an_item_by_name_and_parent()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}"));
        using var client = Client(handler);

        var result = await new ItemTransfer(client, client).RenameAsync(new RemoteItemId("a1"), new RelativePath("Docs/Sub/new.txt"), Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        handler.Calls.Single().Method.ShouldBe(HttpMethod.Patch);
        handler.Calls.Single().Uri.AbsolutePath.ShouldEndWith("me/drive/items/a1");
        handler.Calls.Single().Body.ShouldContain("\"name\":\"new.txt\"");
        handler.Calls.Single().Body.ShouldContain("/drive/root:/Docs/Sub");
    }

    [Fact]
    public async Task rename_a_top_level_item_into_the_drive_root()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}"));
        using var client = Client(handler);

        await new ItemTransfer(client, client).RenameAsync(new RemoteItemId("a1"), new RelativePath("new.txt"), Token);

        handler.Calls.Single().Body.ShouldContain("\"path\":\"/drive/root\"");
    }

    [Fact]
    public async Task delete_an_item()
    {
        var handler = new FakeGraphHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = Client(handler);

        var result = await new ItemTransfer(client, client).DeleteAsync(new RemoteItemId("a1"), Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        handler.Calls.Single().Method.ShouldBe(HttpMethod.Delete);
        handler.Calls.Single().Uri.AbsolutePath.ShouldEndWith("me/drive/items/a1");
    }

    [Fact]
    public async Task treat_deleting_a_missing_item_as_done()
    {
        using var client = Client(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", HttpStatusCode.NotFound)));

        var result = await new ItemTransfer(client, client).DeleteAsync(new RemoteItemId("a1"), Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task create_a_folder_under_its_parent_and_return_its_id()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "id": "folder-1", "name": "Sub Dir", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {} }""", HttpStatusCode.Created));
        using var client = Client(handler);

        var result = await new ItemTransfer(client, client).CreateFolderAsync(new RelativePath("Docs/Sub Dir"), Token);

        result.Value.ShouldBeOfType<Ok<RemoteItemId>>().Value.ShouldBe(new RemoteItemId("folder-1"));
        handler.Calls.Single().Method.ShouldBe(HttpMethod.Post);
        handler.Calls.Single().Uri.AbsolutePath.ShouldEndWith("me/drive/root:/Docs:/children");
        handler.Calls.Single().Body.ShouldContain("\"name\":\"Sub Dir\"");
        handler.Calls.Single().Body.ShouldContain("\"@microsoft.graph.conflictBehavior\":\"fail\"");
    }

    [Fact]
    public async Task create_a_top_level_folder_in_the_drive_root()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "id": "folder-1", "name": "Photos", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {} }""", HttpStatusCode.Created));
        using var client = Client(handler);

        await new ItemTransfer(client, client).CreateFolderAsync(new RelativePath("Photos"), Token);

        handler.Calls.Single().Uri.AbsolutePath.ShouldEndWith("me/drive/root/children");
    }

    [Fact]
    public async Task reuse_the_folder_already_at_the_path()
    {
        var handler = new FakeGraphHandler(request => request.Method == HttpMethod.Post
            ? FakeGraphHandler.Json("{}", HttpStatusCode.Conflict)
            : FakeGraphHandler.Json("""{ "id": "existing-1", "name": "Photos", "size": 0, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "folder": {} }"""));
        using var client = Client(handler);

        var result = await new ItemTransfer(client, client).CreateFolderAsync(new RelativePath("Photos"), Token);

        result.Value.ShouldBeOfType<Ok<RemoteItemId>>().Value.ShouldBe(new RemoteItemId("existing-1"));
        handler.Calls[1].Method.ShouldBe(HttpMethod.Get);
        handler.Calls[1].Uri.AbsolutePath.ShouldEndWith("me/drive/root:/Photos");
    }

    [Fact]
    public async Task refuse_a_folder_whose_name_a_file_already_holds()
    {
        var handler = new FakeGraphHandler(request => request.Method == HttpMethod.Post
            ? FakeGraphHandler.Json("{}", HttpStatusCode.Conflict)
            : FakeGraphHandler.Json("""{ "id": "file-1", "name": "Photos", "size": 3, "lastModifiedDateTime": "2026-01-01T00:00:00Z" }"""));
        using var client = Client(handler);

        var result = await new ItemTransfer(client, client).CreateFolderAsync(new RelativePath("Photos"), Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Permanent>();
    }

    [Fact]
    public async Task map_a_failed_folder_creation_to_a_provider_error()
    {
        using var client = Client(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", HttpStatusCode.NotFound)));

        var result = await new ItemTransfer(client, client).CreateFolderAsync(new RelativePath("Missing/Photos"), Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Permanent>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        using var client = Client(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")));
        var transfer = new ItemTransfer(client, client);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var content = new MemoryStream();

        await Should.ThrowAsync<OperationCanceledException>(() => transfer.DownloadAsync(new RemoteItemId("a"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => transfer.UploadAsync(new UploadRequest(new RelativePath("a"), content, Modified), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => transfer.RenameAsync(new RemoteItemId("a"), new RelativePath("b"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => transfer.DeleteAsync(new RemoteItemId("a"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => transfer.CreateFolderAsync(new RelativePath("a"), cancelled.Token));
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Moves content and changes one drive item at a time through Graph: download, upload, rename and delete.</summary>
/// <param name="graph">Authorised client whose base address is the Graph root, such as <c>https://graph.microsoft.com/v1.0/</c>.</param>
/// <param name="uploads">Client for the pre-authenticated upload session URLs, which must not carry the bearer token.</param>
/// <param name="chunkSize">Bytes per upload session request. Graph needs a multiple of 320 KiB.</param>
internal sealed class ItemTransfer(HttpClient graph, HttpClient uploads, int chunkSize = ItemTransfer.DefaultChunkSize)
{
    /// <summary>Files up to this many bytes go in a single request; larger ones use an upload session.</summary>
    public const int SimpleUploadLimit = 4 * 1024 * 1024;

    /// <summary>Bytes per upload session request: 32 times 320 KiB, as Graph requires a multiple of 320 KiB.</summary>
    public const int DefaultChunkSize = 10 * 1024 * 1024;

    private const string RootParent = "/drive/root";
    private const string ReplaceExisting = "replace";

    /// <summary>Opens the content of one item.</summary>
    /// <returns>The content stream, which the caller disposes, or the <see cref="ProviderError"/> met. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<Stream, ProviderError>> DownloadAsync(RemoteItemId itemId, CancellationToken cancellationToken)
        => GuardAsync(token => OpenAsync(itemId, token), cancellationToken);

    /// <summary>Uploads one file in a single request when it is small, otherwise through an upload session, then makes the remote modified time match.</summary>
    /// <returns>Success, or the <see cref="ProviderError"/> met. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<Unit, ProviderError>> UploadAsync(UploadRequest request, CancellationToken cancellationToken)
        => GuardAsync(token => request.Content.Length <= SimpleUploadLimit ? PutAsync(request, token) : PutInChunksAsync(request, token), cancellationToken);

    /// <summary>Gives an item a new name and parent folder.</summary>
    /// <returns>Success, or the <see cref="ProviderError"/> met. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<Unit, ProviderError>> RenameAsync(RemoteItemId itemId, RelativePath path, CancellationToken cancellationToken)
        => GuardAsync(token => PatchAsync(ItemUrl(itemId), new { name = NameOf(path), parentReference = new { path = ParentOf(path) } }, token), cancellationToken);

    /// <summary>Deletes an item.</summary>
    /// <returns>Success, also when Graph answers 404 because the item is already gone, or the <see cref="ProviderError"/> met. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<Unit, ProviderError>> DeleteAsync(RemoteItemId itemId, CancellationToken cancellationToken)
        => GuardAsync(token => RemoveAsync(itemId, token), cancellationToken);

    private static string ItemUrl(RemoteItemId itemId) => $"me/drive/items/{Uri.EscapeDataString(itemId.Value)}";

    private static string PathUrl(RelativePath path) => $"me/drive/root:/{string.Join('/', path.Value.Split('/').Select(Uri.EscapeDataString))}";

    private static string NameOf(RelativePath path) => path.Value[(path.Value.LastIndexOf('/') + 1)..];

    private static string ParentOf(RelativePath path)
    {
        var slash = path.Value.LastIndexOf('/');

        return slash < 0 ? RootParent : $"{RootParent}:/{path.Value[..slash]}";
    }

    private static Result<Unit, ProviderError> Done() => new Ok<Unit>(default);

    private static async Task<Result<T, ProviderError>> GuardAsync<T>(Func<CancellationToken, Task<Result<T, ProviderError>>> operation, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(operation, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            Success<Result<T, ProviderError>> success => success.Value,
            Failure<Result<T, ProviderError>> failure => new Fail<ProviderError>(AuthenticationErrors.From(failure.Exception))
        };
    }

    private async Task<Result<Stream, ProviderError>> OpenAsync(RemoteItemId itemId, CancellationToken cancellationToken)
    {
        var response = await graph.GetAsync($"{ItemUrl(itemId)}/content", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode) return new Ok<Stream>(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));

        using (response) return new Fail<ProviderError>(GraphErrors.From(response));
    }

    private async Task<Result<Unit, ProviderError>> PutAsync(UploadRequest request, CancellationToken cancellationToken)
    {
        using var content = new StreamContent(request.Content);
        using var response = await graph.PutAsync($"{PathUrl(request.Path)}:/content?@microsoft.graph.conflictBehavior={ReplaceExisting}", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return new Fail<ProviderError>(GraphErrors.From(response));

        var created = await response.Content.ReadFromJsonAsync<GraphDriveItem>(GraphJson.Options, cancellationToken).ConfigureAwait(false);

        return created is null
            ? new Fail<ProviderError>(new Permanent("Graph did not describe the uploaded item."))
            : await PatchAsync(ItemUrl(new RemoteItemId(created.Id)), new { fileSystemInfo = new { lastModifiedDateTime = request.ModifiedUtc } }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Unit, ProviderError>> PutInChunksAsync(UploadRequest request, CancellationToken cancellationToken)
    {
        var item = new Dictionary<string, object>
        {
            ["@microsoft.graph.conflictBehavior"] = ReplaceExisting,
            ["fileSystemInfo"] = new { lastModifiedDateTime = request.ModifiedUtc }
        };
        using var response = await graph.PostAsJsonAsync($"{PathUrl(request.Path)}:/createUploadSession", new { item }, GraphJson.Options, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return new Fail<ProviderError>(GraphErrors.From(response));

        var session = await response.Content.ReadFromJsonAsync<GraphUploadSession>(GraphJson.Options, cancellationToken).ConfigureAwait(false);

        return session is null
            ? new Fail<ProviderError>(new Permanent("Graph did not open an upload session."))
            : await SendChunksAsync(session.UploadUrl, request.Content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Unit, ProviderError>> SendChunksAsync(string uploadUrl, Stream content, CancellationToken cancellationToken)
    {
        var total = content.Length;
        var buffer = new byte[chunkSize];

        for (long offset = 0; offset < total;)
        {
            var read = await content.ReadAtLeastAsync(buffer, (int)Math.Min(chunkSize, total - offset), false, cancellationToken).ConfigureAwait(false);
            if (read == 0) return new Fail<ProviderError>(new Permanent("The file ended before its announced length."));

            using var chunk = new ByteArrayContent(buffer, 0, read);
            chunk.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + read - 1, total);
            using var response = await uploads.PutAsync(uploadUrl, chunk, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new Fail<ProviderError>(GraphErrors.From(response));

            offset += read;
        }

        return Done();
    }

    private async Task<Result<Unit, ProviderError>> PatchAsync(string url, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body, options: GraphJson.Options) };
        using var response = await graph.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return response.IsSuccessStatusCode ? Done() : new Fail<ProviderError>(GraphErrors.From(response));
    }

    private async Task<Result<Unit, ProviderError>> RemoveAsync(RemoteItemId itemId, CancellationToken cancellationToken)
    {
        using var response = await graph.DeleteAsync(ItemUrl(itemId), cancellationToken).ConfigureAwait(false);

        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound ? Done() : new Fail<ProviderError>(GraphErrors.From(response));
    }
}

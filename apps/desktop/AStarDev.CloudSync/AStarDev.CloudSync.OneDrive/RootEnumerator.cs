using System.Net.Http.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Lists the children of a drive folder, or the changes since a delta link, through Graph.</summary>
/// <param name="client">Authorised client whose base address is the Graph root, such as <c>https://graph.microsoft.com/v1.0/</c>.</param>
/// <param name="telemetry">Receives a span and a request count per page.</param>
internal sealed class RootEnumerator(HttpClient client, OneDriveTelemetry telemetry)
{
    private readonly PathResolver resolver = new(client, telemetry);

    /// <summary>Fetches one page of the whole tree under a remote root through Graph's delta endpoint, which lists every descendant flat. Paths are built from parent ids and are relative to <paramref name="root"/>. The resume token is Graph's next link, used as the request URL.</summary>
    /// <returns>The page, with the final page carrying Graph's delta link as the delta token, or the <see cref="ProviderError"/> met. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(RemoteItemId root, string? resumeToken, CancellationToken cancellationToken)
        => await GetPageAsync(resumeToken ?? $"me/drive/items/{Uri.EscapeDataString(root.Value)}/delta", cancellationToken).ConfigureAwait(false) switch
        {
            Ok<GraphChildrenPage> ok => await resolver.ResolveAsync(root, ok.Value.Value ?? [], cancellationToken).ConfigureAwait(false) switch
            {
                Ok<IReadOnlyDictionary<string, string>> paths => new Ok<ProviderSnapshotPage>(DeltaMapping.ToSnapshotPage(root, ok.Value, paths.Value)),
                Fail<ProviderError> fail => fail
            },
            Fail<ProviderError> fail => fail
        };

    /// <summary>Fetches one page of changes. Graph's delta links and next links are absolute URLs, so the delta token and resume token are used as the request URL.</summary>
    /// <returns>The page, or the <see cref="ProviderError"/> met; a 410 is <see cref="InvalidDeltaToken"/>. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(RemoteItemId root, DeltaToken token, string? resumeToken, CancellationToken cancellationToken)
        => await GetPageAsync(resumeToken ?? token.Value, cancellationToken).ConfigureAwait(false) switch
        {
            Ok<GraphChildrenPage> ok => await resolver.ResolveAsync(root, ok.Value.Value ?? [], cancellationToken).ConfigureAwait(false) switch
            {
                Ok<IReadOnlyDictionary<string, string>> paths => new Ok<ProviderDeltaPage>(DeltaMapping.ToDeltaPage(ok.Value, paths.Value)),
                Fail<ProviderError> fail => fail
            },
            Fail<ProviderError> fail => fail
        };

    /// <summary>Fetches every page of children of one folder.</summary>
    /// <returns>All children, or the first <see cref="ProviderError"/> met; items from earlier pages are dropped on failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ListChildrenAsync(RemoteItemId parent, CancellationToken cancellationToken)
        => CollectAsync($"me/drive/items/{Uri.EscapeDataString(parent.Value)}/children", [], cancellationToken);

    private async Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> CollectAsync(string url, List<ProviderItem> items, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync(url, cancellationToken).ConfigureAwait(false);

        return page switch
        {
            Fail<ProviderError> fail => fail,
            Ok<GraphChildrenPage> ok => await ContinueAsync(ok.Value, items, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<Result<IReadOnlyList<ProviderItem>, ProviderError>> ContinueAsync(GraphChildrenPage page, List<ProviderItem> items, CancellationToken cancellationToken)
    {
        items.AddRange((page.Value ?? []).Select(DriveItemMapping.ToProviderItem));

        return page.NextLink is { } next
            ? await CollectAsync(next, items, cancellationToken).ConfigureAwait(false)
            : new Ok<IReadOnlyList<ProviderItem>>(items);
    }

    private async Task<Result<GraphChildrenPage, ProviderError>> GetPageAsync(string url, CancellationToken cancellationToken)
    {
        using var span = telemetry.StartPage();
        telemetry.RecordRequest();
        var outcome = await Try.RunAsync(token => FetchAsync(url, token), cancellationToken).ConfigureAwait(false);
        Result<GraphChildrenPage, ProviderError> page = outcome switch
        {
            Success<Result<GraphChildrenPage, ProviderError>> success => success.Value,
            Failure<Result<GraphChildrenPage, ProviderError>> failure => new Fail<ProviderError>(AuthenticationErrors.From(failure.Exception))
        };

        if (page.Value is Fail<ProviderError> failed) RecordFailure(failed.Error);

        return page;
    }

    private async Task<Result<GraphChildrenPage, ProviderError>> FetchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return new Fail<ProviderError>(GraphErrors.From(response));

        var page = await response.Content.ReadFromJsonAsync<GraphChildrenPage>(GraphJson.Options, cancellationToken).ConfigureAwait(false);

        return new Ok<GraphChildrenPage>(page ?? new GraphChildrenPage(null, null));
    }

    private void RecordFailure(ProviderError error)
    {
        telemetry.RecordFailure();
        if (error.Value is RateLimited) telemetry.RecordRateLimit();
    }
}

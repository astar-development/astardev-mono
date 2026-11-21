using System.Net.Http.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Lists the children of a drive folder through Graph, following next links until the last page.</summary>
/// <param name="client">Authorised client whose base address is the Graph root, such as <c>https://graph.microsoft.com/v1.0/</c>.</param>
/// <param name="telemetry">Receives a span and a request count per page.</param>
internal sealed class RootEnumerator(HttpClient client, OneDriveTelemetry telemetry)
{
    /// <summary>Fetches every page under the scope's root and returns the items as a single <see cref="ProviderSnapshotPage"/>.</summary>
    /// <returns>All items, or the first <see cref="ProviderError"/> met; items from earlier pages are dropped on failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken)
        => CollectAsync($"me/drive/items/{Uri.EscapeDataString(scope.RemoteRootId.Value)}/children", [], cancellationToken);

    private async Task<Result<ProviderSnapshotPage, ProviderError>> CollectAsync(string url, List<ProviderItem> items, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync(url, cancellationToken).ConfigureAwait(false);

        return page switch
        {
            Fail<ProviderError> fail => fail,
            Ok<GraphChildrenPage> ok => await ContinueAsync(ok.Value, items, cancellationToken).ConfigureAwait(false)
        };
    }

    private async Task<Result<ProviderSnapshotPage, ProviderError>> ContinueAsync(GraphChildrenPage page, List<ProviderItem> items, CancellationToken cancellationToken)
    {
        items.AddRange((page.Value ?? []).Select(DriveItemMapping.ToProviderItem));

        return page.NextLink is { } next
            ? await CollectAsync(next, items, cancellationToken).ConfigureAwait(false)
            : new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage(items));
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

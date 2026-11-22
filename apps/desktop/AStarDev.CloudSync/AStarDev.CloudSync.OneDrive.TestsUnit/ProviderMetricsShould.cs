using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class ProviderMetricsShould
{
    private static readonly DateTimeOffset Modified = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task count_upload_download_and_delta_requests()
    {
        using var telemetry = new OneDriveTelemetry();
        var counts = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter == telemetry.Meter) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name != ProviderMetrics.RequestsName) return;

            var operation = tags.ToArray().Single(tag => tag.Key == ProviderMetrics.OperationAttribute).Value!.ToString()!;
            counts[operation] = counts.GetValueOrDefault(operation) + value;
        });
        listener.Start();
        var handler = new FakeGraphHandler(request => request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/content", StringComparison.Ordinal)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("hello") }
            : FakeGraphHandler.Json("""{ "id": "created", "value": [] }"""));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };
        var transfer = new ItemTransfer(client, client, ItemTransfer.DefaultChunkSize, telemetry.Provider);
        var enumerator = new RootEnumerator(client, telemetry);

        var downloaded = await transfer.DownloadAsync(new RemoteItemId("a"), Token);
        await using var content = downloaded.Value.ShouldBeOfType<Ok<Stream>>().Value;
        await transfer.UploadAsync(new UploadRequest(new RelativePath("a.txt"), new MemoryStream([1, 2, 3]), Modified), Token);
        await transfer.UploadAsync(new UploadRequest(new RelativePath("b.txt"), new MemoryStream([4]), Modified), Token);
        await enumerator.GetDeltasAsync(new RemoteItemId("root"), new DeltaToken("https://graph.test/delta?token=old"), null, Token);

        counts.ShouldBe(new Dictionary<string, long> { ["download"] = 1, ["upload"] = 2, ["delta"] = 1 });
    }
}

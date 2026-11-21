using System.Diagnostics;
using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveTelemetryShould
{
    [Fact]
    public async Task record_a_span_and_a_request_count_per_page()
    {
        using var telemetry = new OneDriveTelemetry();
        var spans = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source == telemetry.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(activityListener);
        var requests = 0L;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter == telemetry.Meter) listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == OneDriveTelemetry.RequestsName) requests += value;
        });
        meterListener.Start();
        var handler = new FakeGraphHandler(request => request.RequestUri!.AbsoluteUri.Contains("page=2")
            ? FakeGraphHandler.Json("""{ "value": [] }""")
            : FakeGraphHandler.Json("""{ "value": [], "@odata.nextLink": "https://graph.test/next?page=2" }"""));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };

        await new RootEnumerator(client, telemetry).GetSnapshotAsync(new RemoteItemId("root"), TestContext.Current.CancellationToken);

        spans.Count.ShouldBe(2);
        requests.ShouldBe(2);
    }
}

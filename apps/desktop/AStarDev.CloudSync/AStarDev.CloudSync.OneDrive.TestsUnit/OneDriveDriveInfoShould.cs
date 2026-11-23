using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveDriveInfoShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task map_the_graph_quota_to_drive_info()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "quota": { "total": 100, "used": 30, "remaining": 70, "state": "normal" } }"""));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.GetDriveInfoAsync(Token);

        var info = result.Value.ShouldBeOfType<Ok<ProviderDriveInfo>>().Value;
        (info.UsedBytes, info.RemainingBytes, info.TotalBytes).ShouldBe((30L, 70L, 100L));
        info.ItemCountEstimate.ShouldBe(Option.None<long>());
        handler.Requests.Single().AbsolutePath.ShouldEndWith("me/drive");
    }

    [Fact]
    public async Task return_a_provider_error_when_graph_fails()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", System.Net.HttpStatusCode.Unauthorized));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);

        var result = await enumerator.GetDriveInfoAsync(Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}"));
        using var telemetry = new OneDriveTelemetry();
        var enumerator = new RootEnumerator(Client(handler), telemetry);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => enumerator.GetDriveInfoAsync(cancelled.Token));
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };
}

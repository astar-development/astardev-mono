using System.Net;
using System.Net.Http.Headers;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class GraphErrorsShould
{
    [Fact]
    public async Task map_429_to_a_rate_limited_error_with_retry_after()
    {
        var response = FakeGraphHandler.Json("{}", HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(17));

        var error = await FailWith(response);

        error.ShouldBeOfType<RateLimited>().RetryAfter.ShouldBe(TimeSpan.FromSeconds(17));
    }

    [Fact]
    public async Task map_503_to_a_transient_error()
    {
        var error = await FailWith(FakeGraphHandler.Json("{}", HttpStatusCode.ServiceUnavailable));

        error.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task map_401_to_an_authentication_failed_error()
    {
        var error = await FailWith(FakeGraphHandler.Json("{}", HttpStatusCode.Unauthorized));

        error.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task map_403_to_a_permanent_error()
    {
        var error = await FailWith(FakeGraphHandler.Json("{}", HttpStatusCode.Forbidden));

        error.ShouldBeOfType<Permanent>();
    }

    private static async Task<object?> FailWith(HttpResponseMessage response)
    {
        using var telemetry = new OneDriveTelemetry();
        var client = new HttpClient(new FakeGraphHandler(_ => response)) { BaseAddress = new Uri("https://graph.test/v1.0/") };
        var enumerator = new RootEnumerator(client, telemetry);

        var result = await enumerator.GetSnapshotAsync(new RemoteItemId("root"), TestContext.Current.CancellationToken);

        return result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value;
    }
}

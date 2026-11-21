using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Observability.TestsUnit;

public sealed class TelemetryOptionsShould
{
    [Fact]
    public void reject_a_malformed_otlp_endpoint()
    {
        var failure = Should.Throw<OptionsValidationException>(() => TelemetryTestHost.Create("not a uri").Services.GetRequiredService<IOptions<TelemetryOptions>>().Value);

        failure.Failures.ShouldHaveSingleItem().ShouldContain(nameof(TelemetryOptions.OtlpEndpoint));
    }

    [Fact]
    public void reject_an_otlp_endpoint_that_is_not_http_or_https()
    {
        Should.Throw<OptionsValidationException>(() => TelemetryTestHost.Create("ftp://localhost:4317").Services.GetRequiredService<IOptions<TelemetryOptions>>().Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://localhost:4317")]
    [InlineData("https://otel.example.test:4317")]
    public void accept_an_empty_or_absolute_http_endpoint(string endpoint)
    {
        using var host = TelemetryTestHost.Create(endpoint);

        host.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value.OtlpEndpoint.ShouldBe(endpoint);
    }

    [Fact]
    public void default_to_no_endpoint()
    {
        using var host = TelemetryTestHost.Create();

        host.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value.OtlpEndpoint.ShouldBeEmpty();
    }
}

using System.Diagnostics;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Theming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class UiServiceCollectionExtensionsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IHost CreateHost(string? otlpEndpoint = null)
    {
        var builder = Host.CreateApplicationBuilder();
        if (otlpEndpoint is not null) builder.Configuration["OpenTelemetry:OtlpEndpoint"] = otlpEndpoint;
        builder.Services.AddCloudSyncUi();

        return builder.Build();
    }

    [Fact]
    public async Task export_telemetry_as_the_ui_service()
    {
        using var host = CreateHost();
        await host.StartAsync(Token);

        var serviceName = host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value)["service.name"];
        var loggerProviders = host.Services.GetServices<ILoggerProvider>().Select(provider => provider.GetType()).ToList();
        await host.StopAsync(Token);

        serviceName.ShouldBe("astardev-cloudsync-ui");
        loggerProviders.ShouldContain(typeof(OpenTelemetryLoggerProvider));
    }

    [Fact]
    public void compose_localisation_from_the_embedded_feature_files_in_en_gb()
    {
        using var host = CreateHost();

        var localisation = host.Services.GetRequiredService<ILocalisationService>();

        localisation.Language.ShouldBe("en-GB");
        localisation.Get("Common.AppTitle").ShouldBe("AStarDev.CloudSync");
        localisation.Get("Common.Missing").ShouldBe("!Common.Missing!");
    }

    [Fact]
    public void compose_one_culture_service_for_en_gb_formatting()
    {
        using var host = CreateHost();

        var culture = host.Services.GetRequiredService<ICultureService>();

        culture.ShouldBeSameAs(host.Services.GetRequiredService<ICultureService>());
        culture.Culture.Name.ShouldBe("en-GB");
    }

    [Fact]
    public void compose_a_theme_service_with_no_theme_applied_yet()
    {
        using var host = CreateHost();

        host.Services.GetRequiredService<IThemeService>().Current.ShouldBeNull();
    }

    [Fact]
    public async Task collect_the_ipc_spans_of_the_client()
    {
        using var host = CreateHost();
        await host.StartAsync(Token);
        using var source = new ActivitySource("AStarDev.CloudSync.IPC");

        var collected = source.HasListeners();
        await host.StopAsync(Token);

        collected.ShouldBeTrue();
    }

    [Fact]
    public async Task start_with_the_otlp_endpoint_from_the_existing_configuration_key()
    {
        using var host = CreateHost("http://localhost:4317");

        await host.StartAsync(Token);
        host.Services.GetRequiredService<TracerProvider>().ShouldNotBeNull();
        await host.StopAsync(Token);
    }

    [Fact]
    public async Task fail_to_start_when_the_otlp_endpoint_is_malformed() => await Should.ThrowAsync<OptionsValidationException>(async () =>
                                                                                  {
                                                                                      using var host = CreateHost("not a uri");
                                                                                      await host.StartAsync(Token);
                                                                                  });
}

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AStarDev.CloudSync.Observability;

/// <summary>Registers OpenTelemetry logs, metrics and traces for a CloudSync process.</summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers logs, metrics and traces. Every <see cref="System.Diagnostics.ActivitySource"/> and <see cref="System.Diagnostics.Metrics.Meter"/> named
    /// <c>AStarDev.CloudSync.*</c> is collected, so other modules only use <c>System.Diagnostics</c>. Telemetry is exported over OTLP to the
    /// <c>OpenTelemetry:OtlpEndpoint</c> setting; when it is empty nothing is exported. A malformed endpoint makes the host fail when the options are first read.
    /// </summary>
    /// <param name="services">The collection to add the telemetry services to.</param>
    /// <param name="serviceName">The <c>service.name</c> resource attribute that identifies the process in the dashboard.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCloudSyncTelemetry("astardev-cloudsync-daemon");
    /// </code>
    /// </example>
    public static IServiceCollection AddCloudSyncTelemetry(this IServiceCollection services, string serviceName)
    {
        services.AddOptions<TelemetryOptions>().BindConfiguration(TelemetryOptions.ConfigurationSection).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TelemetryOptions>, TelemetryOptionsValidator>());
        services.AddOptions<OpenTelemetryLoggerOptions>().Configure(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: Version(), serviceInstanceId: Guid.NewGuid().ToString()))
            .WithLogging()
            .WithMetrics(metrics => metrics.AddMeter(TelemetryNames.SourcePattern))
            .WithTracing(tracing => tracing.AddSource(TelemetryNames.SourcePattern));

        services.ConfigureOpenTelemetryLoggerProvider((provider, logging) => ExportTo(provider, endpoint => logging.AddProcessor(new BatchLogRecordExportProcessor(new OtlpLogExporter(ExporterOptions(endpoint))))));
        services.ConfigureOpenTelemetryMeterProvider((provider, metrics) => ExportTo(provider, endpoint => metrics.AddReader(new PeriodicExportingMetricReader(new OtlpMetricExporter(ExporterOptions(endpoint))))));
        services.ConfigureOpenTelemetryTracerProvider((provider, tracing) => ExportTo(provider, endpoint => tracing.AddProcessor(new BatchActivityExportProcessor(new OtlpTraceExporter(ExporterOptions(endpoint))))));

        return services;
    }

    private static void ExportTo(IServiceProvider provider, Action<Uri> addExporter)
    {
        var endpoint = provider.GetRequiredService<IOptions<TelemetryOptions>>().Value.OtlpEndpoint;
        if (endpoint.Length > 0) addExporter(new Uri(endpoint));
    }

    private static OtlpExporterOptions ExporterOptions(Uri endpoint) => new() { Endpoint = endpoint };

    private static string Version()
        => typeof(TelemetryServiceCollectionExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}

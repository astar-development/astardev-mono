using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
/// <summary>
/// Shared Aspire baseline that every service project applies once at startup, so telemetry,
/// health checks, service discovery and HTTP resilience behave the same in every service.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>
    /// Applies the whole baseline in one call: OpenTelemetry, the default health checks, service discovery,
    /// and the standard resilience handler on every <see cref="System.Net.Http.HttpClient"/> the service creates.
    /// </summary>
    /// <typeparam name="TBuilder">Any host builder, so web and worker services share the same call.</typeparam>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.AddServiceDefaults();
    /// </code>
    /// </example>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    /// <summary>
    /// Sends logs, metrics and traces through OpenTelemetry. Requests to the health endpoints are left out of
    /// traces so probes do not hide real traffic. Exporters are switched on only when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> or <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is configured.
    /// </summary>
    /// <typeparam name="TBuilder">Any host builder, so web and worker services share the same call.</typeparam>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            _ = builder.Services.AddOpenTelemetry().UseAzureMonitor();

        return builder;
    }

    /// <summary>
    /// Registers a <c>self</c> check tagged <c>live</c>, so the liveness endpoint answers healthy while the process
    /// can respond at all. Checks for real dependencies are added by each service.
    /// </summary>
    /// <typeparam name="TBuilder">Any host builder, so web and worker services share the same call.</typeparam>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Exposes <c>/health</c> (every check must pass) and <c>/alive</c> (only checks tagged <c>live</c>),
    /// in Development only: outside Development nothing is mapped, because health output can leak service details.
    /// </summary>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.MapDefaultEndpoints();
    /// </code>
    /// </example>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}

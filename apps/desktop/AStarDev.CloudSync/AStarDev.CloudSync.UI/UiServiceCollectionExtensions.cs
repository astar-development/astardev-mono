using AStarDev.CloudSync.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI;

/// <summary>The UI's whole service graph in one place, so the application and the tests compose exactly the same services.</summary>
internal static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Registers telemetry, so the UI exports its IPC spans, metrics and logs to the <c>OpenTelemetry:OtlpEndpoint</c> setting the daemon uses. The UI
    /// span of a request and the daemon span it caused then share one trace. An empty endpoint exports nothing, and a malformed one fails the host when it starts.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder();
    /// builder.Services.AddCloudSyncUi();
    /// await builder.Build().StartAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddCloudSyncUi(this IServiceCollection services)
    {
        services.AddCloudSyncTelemetry(ApplicationConstants.ServiceName);

        return services;
    }
}

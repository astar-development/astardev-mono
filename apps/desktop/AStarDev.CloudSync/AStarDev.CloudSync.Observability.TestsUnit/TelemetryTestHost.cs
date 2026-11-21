using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Observability.TestsUnit;

internal static class TelemetryTestHost
{
    public const string ServiceName = "astardev-cloudsync-tests";

    public static IHost Create(string? otlpEndpoint = null)
    {
        var builder = Host.CreateApplicationBuilder();
        if (otlpEndpoint is not null) builder.Configuration[$"{TelemetryOptions.ConfigurationSection}:{nameof(TelemetryOptions.OtlpEndpoint)}"] = otlpEndpoint;
        builder.Services.AddCloudSyncTelemetry(ServiceName);

        return builder.Build();
    }
}

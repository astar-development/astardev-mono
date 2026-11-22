using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Observability.TestsUnit;

internal static class TelemetryTestHost
{
    public const string ServiceName = "astardev-cloudsync-tests";

    public static IHost Create(string? otlpEndpoint = null, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateApplicationBuilder();
        foreach (var pair in settings ?? new Dictionary<string, string?>()) builder.Configuration[pair.Key] = pair.Value;
        if (otlpEndpoint is not null) builder.Configuration[$"{TelemetryOptions.ConfigurationSection}:{nameof(TelemetryOptions.OtlpEndpoint)}"] = otlpEndpoint;
        builder.Services.AddCloudSyncTelemetry(ServiceName);

        return builder.Build();
    }
}

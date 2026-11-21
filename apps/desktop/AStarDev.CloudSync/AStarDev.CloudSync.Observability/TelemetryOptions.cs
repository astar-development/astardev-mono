namespace AStarDev.CloudSync.Observability;

/// <summary>The <c>OpenTelemetry</c> configuration section: where telemetry is exported.</summary>
internal sealed class TelemetryOptions
{
    /// <summary>The configuration section the options bind to.</summary>
    public const string ConfigurationSection = "OpenTelemetry";

    /// <summary>
    /// The OTLP endpoint that receives logs, metrics and traces, for example the Aspire dashboard. Empty means nothing is exported, so the
    /// daemon runs without AppHost. A non-empty value that is not an absolute http or https URI fails options validation when first read.
    /// </summary>
    public string OtlpEndpoint { get; set; } = string.Empty;
}

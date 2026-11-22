using System.Diagnostics.Metrics;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The per-operation provider request counter of doc 12, created on the meter of <see cref="OneDriveTelemetry"/>.</summary>
internal sealed class ProviderMetrics
{
    /// <summary>Instrument name of the counter of provider requests by operation.</summary>
    public const string RequestsName = "cloudsync.provider.requests";

    /// <summary>Attribute that carries the operation of a request.</summary>
    public const string OperationAttribute = "cloudsync.provider.operation";

    private readonly Counter<long> requests;

    /// <summary>Creates the counter on the given meter.</summary>
    /// <param name="meter">The meter that owns the counter, named so OpenTelemetry collects it.</param>
    public ProviderMetrics(Meter meter) => requests = meter.CreateCounter<long>(RequestsName, "{request}", "Provider requests sent, by operation: delta, upload or download.");

    /// <summary>Counts one delta page request.</summary>
    public void CountDelta() => Count("delta");

    /// <summary>Counts one upload; a chunked upload counts once, not per chunk.</summary>
    public void CountUpload() => Count("upload");

    /// <summary>Counts one download.</summary>
    public void CountDownload() => Count("download");

    private void Count(string operation) => requests.Add(1, new KeyValuePair<string, object?>(OperationAttribute, operation));
}

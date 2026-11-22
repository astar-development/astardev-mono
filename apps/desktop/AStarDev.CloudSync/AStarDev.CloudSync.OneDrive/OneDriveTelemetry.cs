using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The spans and provider metrics (doc 12) for OneDrive requests. Register one instance for the process.</summary>
internal sealed class OneDriveTelemetry : IDisposable
{
    /// <summary>Name of both the <see cref="ActivitySource"/> and the <see cref="Meter"/>, for OpenTelemetry to subscribe to.</summary>
    public const string SourceName = "AStarDev.CloudSync.OneDrive";

    /// <summary>Instrument name of the counter of Graph requests sent.</summary>
    public const string RequestsName = "onedrive.requests";

    /// <summary>Instrument name of the counter of requests that ended in a <see cref="Ports.ProviderError"/>.</summary>
    public const string FailuresName = "onedrive.failures";

    /// <summary>Instrument name of the counter of 429 answers.</summary>
    public const string RateLimitsName = "onedrive.rate_limits";

    private readonly Counter<long> requests;
    private readonly Counter<long> failures;
    private readonly Counter<long> rateLimits;

    /// <summary>Creates the source and meter with their instruments.</summary>
    public OneDriveTelemetry()
    {
        ActivitySource = new ActivitySource(SourceName);
        Meter = new Meter(SourceName);
        requests = Meter.CreateCounter<long>(RequestsName, "{request}", "Graph requests sent");
        failures = Meter.CreateCounter<long>(FailuresName, "{request}", "Graph requests that failed");
        rateLimits = Meter.CreateCounter<long>(RateLimitsName, "{event}", "Graph rate limit responses");
        Provider = new ProviderMetrics(Meter);
    }

    /// <summary>Counters of delta, upload and download requests.</summary>
    public ProviderMetrics Provider { get; }

    /// <summary>Source of the spans, one per page fetched.</summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>Owner of the counters.</summary>
    public Meter Meter { get; }

    /// <summary>Starts the span for one page request. <c>null</c> when nothing listens; dispose it to end the span.</summary>
    public Activity? StartPage() => ActivitySource.StartActivity("OneDrive.GetChildrenPage", ActivityKind.Client);

    /// <summary>Counts one request sent.</summary>
    public void RecordRequest() => requests.Add(1);

    /// <summary>Counts one failed request.</summary>
    public void RecordFailure() => failures.Add(1);

    /// <summary>Counts one rate limit answer.</summary>
    public void RecordRateLimit() => rateLimits.Add(1);

    /// <inheritdoc/>
    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}

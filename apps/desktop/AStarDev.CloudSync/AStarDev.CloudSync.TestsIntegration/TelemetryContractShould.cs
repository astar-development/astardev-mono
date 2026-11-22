using System.Diagnostics;
using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.IPC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class TelemetryContractShould(TelemetryRunFixture fixture) : IClassFixture<TelemetryRunFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TelemetryRun Run => fixture.Run;

    [Fact]
    public void emit_every_documented_metric()
    {
        Run.Metrics.Published.Order().ShouldBe(TelemetryContract.Metrics.Order());
        Run.Metrics.Measured.Order().ShouldBe(TelemetryContract.Metrics.Order());
    }

    [Fact]
    public void emit_the_workflow_spans_with_correlation_ids()
    {
        var spans = Run.Spans.All;
        TelemetryContract.Spans.ShouldAllBe(name => spans.Any(span => span.OperationName == name));
        var downloadTrace = TraceOf(spans, "operations process", "DownloadFile", Run.DownloadedItem.Value.ToString());
        var inTrace = spans.Where(span => span.TraceId == downloadTrace).Select(span => span.OperationName).ToList();
        inTrace.ShouldContain("cloudsync.item.plan");
        inTrace.ShouldContain("cloudsync.reconcile");
        inTrace.ShouldContain("cloudsync.operation.plan");
        inTrace.ShouldContain("operations publish");
        inTrace.ShouldContain("operations process");
        inTrace.ShouldContain("cloudsync.provider.request");
        inTrace.ShouldContain("cloudsync.snapshot.commit");
        var failed = spans.Where(span => span.OperationName == "operations process" && span.Status == ActivityStatusCode.Error).ShouldHaveSingleItem();
        failed.GetTagItem("error.type").ShouldBe("Permanent");
        var resolution = spans.Single(span => span.OperationName == "cloudsync.conflict.resolve");
        spans.ShouldContain(span => span.OperationName == "cloudsync.operation.plan" && span.TraceId == resolution.TraceId && span.ParentSpanId == resolution.SpanId);
        var scope = spans.Single(span => span.OperationName == "cloudsync.delta.process" && span.GetTagItem("cloudsync.delta.changes") is 1);
        spans.ShouldContain(span => span.OperationName == "cloudsync.delta.page" && span.TraceId == scope.TraceId);
        var client = spans.First(span => span.Kind == ActivityKind.Client && span.GetTagItem("rpc.system") is "cloudsync-ipc");
        spans.ShouldContain(span => span.Kind == ActivityKind.Server && span.ParentSpanId == client.SpanId && span.TraceId == client.TraceId);

        var job = Run.Job.Value.ToString();
        var operationLogs = Run.Logs.Entries.Where(entry => entry.Category == "AStarDev.CloudSync.SyncEngine.OperationScheduler" && entry.EventId is 1002 or 1005).ToList();
        operationLogs.Count.ShouldBeGreaterThanOrEqualTo(2);
        foreach (var entry in operationLogs)
        {
            entry.ScopeValue("OperationId").ShouldNotBeNull();
            entry.ScopeValue("ItemId").ShouldNotBeNull();
            entry.ScopeValue("SyncJobId").ShouldBe(job);
            entry.TraceId.ShouldNotBe(default);
        }

        operationLogs.ShouldContain(entry => entry.EventId == 1002 && entry.TraceId == downloadTrace);
        foreach (var eventId in TelemetryContract.StateTransitionLogEvents.Where(id => id is >= 1100 and <= 1105))
        {
            var entry = Run.Logs.Entries.FirstOrDefault(candidate => candidate.EventId == eventId);
            entry.ShouldNotBeNull($"no log with event id {eventId}");
            (entry.State["SyncJobId"]?.ToString()).ShouldBe(job);
        }
    }

    [Fact]
    public void keep_domain_free_of_telemetry()
    {
        var references = typeof(Domain.Account).Assembly.GetReferencedAssemblies().Select(name => name.Name!).ToList();

        references.ShouldNotContain(name => name.StartsWith("OpenTelemetry", StringComparison.Ordinal) || name.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal) || name == "System.Diagnostics.DiagnosticSource");
    }

    [Fact]
    public async Task run_without_apphost()
    {
        Environment.GetEnvironmentVariables().Keys.Cast<string>().ShouldNotContain(name => name.StartsWith("ASPIRE_", StringComparison.Ordinal));
        typeof(DaemonStatusView).Assembly.GetReferencedAssemblies().Concat(typeof(TelemetryRun).Assembly.GetReferencedAssemblies()).ShouldNotContain(name => name.Name!.StartsWith("Aspire", StringComparison.Ordinal));
        await using var standalone = new TelemetryRun();
        standalone.Services.GetRequiredService<IConfiguration>()["OpenTelemetry:OtlpEndpoint"].ShouldBeNullOrEmpty();

        await standalone.StartAsync(Token);
        var status = await standalone.QueryStatusAsync(Token);
        await standalone.StopAsync(Token);

        status.State.ShouldBe(DaemonState.Running);
        standalone.Metrics.Measured.ShouldContain("cloudsync.ipc.requests");
        standalone.Spans.All.Any(span => span.GetTagItem("rpc.system") is "cloudsync-ipc").ShouldBeTrue();
    }

    [Fact]
    public async Task honour_cancellation_when_driving_the_daemon()
    {
        await using var cancelledRun = new TelemetryRun();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => cancelledRun.DriveAsync(cancelled.Token));
    }

    private static ActivityTraceId TraceOf(IReadOnlyList<Activity> spans, string name, string operationType, string itemId)
        => spans.Single(span => span.OperationName == name && span.GetTagItem("cloudsync.operation.type") is string type && type == operationType && span.GetTagItem("cloudsync.item.id") is string id && id == itemId && span.Status != ActivityStatusCode.Error).TraceId;
}

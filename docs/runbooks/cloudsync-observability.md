# CloudSync observability runbook

How to see what the CloudSync daemon and UI are doing. Design: `docs/architecture/12-observability-and-telemetry-strategy.md`.
The list of metrics and spans below is enforced by `TelemetryContractShould` in `AStarDev.CloudSync.TestsIntegration`;
the single source for the expected names is `TelemetryContract.cs` there.

## Start with the Aspire dashboard

```bash
dotnet run --project apps/Aspire/AStarDev.AppHost
```

- AppHost starts the daemon (resource `CloudSyncDaemon`) and then the UI (resource `CloudSync`).
- The dashboard opens at `http://localhost:15144` (profile `http`) or `https://localhost:17274` (profile `https`); the login link is printed in the console.
- AppHost passes its dashboard OTLP address to both processes as `OpenTelemetry__OtlpEndpoint`, and Aspire adds `OTEL_SERVICE_NAME`,
  `OTEL_RESOURCE_ATTRIBUTES` and the OTLP API key header. The processes take their resource name and instance id from those, so telemetry
  appears under `CloudSyncDaemon` and `CloudSync`, next to their console output.

## Where to look

| Question | Dashboard page | What to select |
| --- | --- | --- |
| What happened? | Structured logs | Resource `CloudSyncDaemon`; filter on `SyncJobId`, `OperationId`, `ItemId` or `ConflictId` |
| How much? | Metrics | Resource `CloudSyncDaemon`, meter `AStarDev.CloudSync.*`, instrument from the list below |
| How did it happen? | Traces | Resource `CloudSyncDaemon`; open a trace, or follow a log's trace link |

A log written inside a span carries the trace id, so the log row links to its trace and back.

## Run the daemon without AppHost

AppHost and Aspire are not needed. With no OTLP endpoint configured the daemon exports nothing and works as usual.

To watch it anyway, start any OTLP/gRPC receiver, for example the standalone dashboard:

```bash
docker run --rm -p 18888:18888 -p 4317:18889 -e DASHBOARD__FRONTEND__AUTHMODE=Unsecured -e DASHBOARD__OTLP__AUTHMODE=Unsecured mcr.microsoft.com/dotnet/aspire-dashboard:latest
OpenTelemetry__OtlpEndpoint=http://localhost:4317 dotnet run --project apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon
```

Open `http://localhost:18888`. The resource is named `astardev-cloudsync-daemon` unless `OTEL_SERVICE_NAME` is set.
The value must be an absolute http or https URI; anything else stops the daemon at start.

## Correlation identifiers

Logs carry these as structured properties (scope or message state): `SyncJobId`, `OperationId`, `ItemId`, `AccountId`, `RemoteItemId`, `ConflictId`.
Spans carry the same ids as tags: `cloudsync.sync_job.id`, `cloudsync.item.id`, `messaging.message.id` (the operation id), `cloudsync.conflict.id`.

One logical operation is one trace: `cloudsync.item.plan` (linked to its `cloudsync.plan` run), `cloudsync.reconcile`, `cloudsync.operation.plan`,
`operations publish`, then `operations process` with `cloudsync.provider.request` and `cloudsync.snapshot.commit`. The queued operation stores its trace parent,
so execution continues the planner's trace even after a restart. A failed operation has an error status and the `error.type` tag on `operations process`.
IPC requests continue the caller's trace: the UI's client span is the parent of the daemon's server span.

State transition log events: 1100 sync started, 1101 sync completed, 1102 conflict detected, 1103 conflict resolved, 1104 delta token refreshed,
1105 delta processing finished, 1106 delta token invalid, 1001 to 1006 operation lifecycle (1002 completed, 1005 failed and dead-lettered).

## Metrics

| Instrument | Meaning |
| --- | --- |
| `cloudsync.queue.pending`, `cloudsync.queue.running`, `cloudsync.queue.dead_lettered` | Queue depth gauges |
| `cloudsync.queue.operation.duration`, `cloudsync.queue.operation.latency` | Execution time and wait before the first attempt |
| `cloudsync.queue.operation.completed`, `cloudsync.queue.operation.failed` | Operation results, by operation type (failed also by error type) |
| `cloudsync.sync.jobs.active` | Sync jobs running in the background |
| `cloudsync.sync.items_synchronised` | Items brought in step, by operation type |
| `cloudsync.sync.conflicts.open` | Conflicts waiting for the user, as tracked since the daemon started |
| `cloudsync.sync.conflict.resolutions` | Conflicts resolved, by resolution |
| `cloudsync.syncengine.items_indexed`, `cloudsync.syncengine.crawl.duration` | Initial crawl progress and time per scope |
| `cloudsync.performance.reconciliation.duration`, `cloudsync.performance.delta.duration` | Time to plan a job, time to apply a scope's delta pages |
| `cloudsync.provider.requests` | Provider requests by `cloudsync.provider.operation`: delta, upload, download |
| `onedrive.requests`, `onedrive.failures`, `onedrive.rate_limits` | Graph requests, failures, 429 answers |
| `cloudsync.persistence.saves`, `cloudsync.persistence.save.duration` | Database saves and their time |
| `cloudsync.ipc.requests`, `cloudsync.ipc.request.duration`, `cloudsync.ipc.connections` | IPC requests, their time, open connections, by side |

## Spans

`crawl.scope`, `crawl.page`, `cloudsync.plan`, `cloudsync.item.plan`, `cloudsync.reconcile`, `cloudsync.operation.plan`, `operations publish`, `operations process`,
`cloudsync.provider.request`, `cloudsync.snapshot.commit`, `cloudsync.delta.process`, `cloudsync.delta.page`, `cloudsync.conflict.resolve`, `persistence.save`,
`OneDrive.GetChildrenPage`, and one span per IPC message named `<kind> <message type>` (for example `query GetDaemonStatus`).

## Adding telemetry

Add the instrument or span name to `TelemetryContract.cs` in `AStarDev.CloudSync.TestsIntegration`, make `TelemetryRun` drive it, and update this runbook.
Domain must stay free of telemetry types; modules other than Observability use only `System.Diagnostics` and are collected by the `AStarDev.CloudSync.*` name pattern.

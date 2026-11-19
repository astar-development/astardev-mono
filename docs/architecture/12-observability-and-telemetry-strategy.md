# 12 Observability and Telemetry Strategy

## Architectural Principles

### If AStarDev.CloudSync Is Doing Work, That Work Must Be Observable

AStarDev.CloudSync must never appear idle while meaningful work is occurring.

Users should always be able to understand:

- What is happening
- Why it is happening
- Whether progress is being made
- Whether intervention is required

---

### Observability Is Part Of The Architecture

Observability is not a support feature or an afterthought.

Instrumentation should be designed and implemented alongside features.

---

### Every Important State Transition Produces Telemetry

Examples:

```text
Sync Started
Sync Completed
Conflict Detected
Operation Queued
Operation Started
Operation Completed
Operation Failed
Delta Token Refreshed
```

---

### Structured Data Over Free Text

Prefer:

```text
OperationId=123
ItemId=ABC
SyncJobId=XYZ
```

over:

```text
Failed uploading file.
```

---

## OpenTelemetry Semantic Conventions First

AStarDev.CloudSync adopts OpenTelemetry Semantic Conventions wherever practical.

Reference:

```text
https://opentelemetry.io/docs/specs/semconv/
```

Custom attributes should only be introduced when no appropriate semantic convention exists.

Principle:

```text
Use standards where available.
Extend only when necessary.
```

---

## Stable Correlation Identifiers

Logs, metrics and traces should include identifiers wherever available.

Examples:

```text
SyncJobId
OperationId
ItemId
RemoteItemId
AccountId
ConflictId
ScopeId
```

These identifiers allow operators to move between:

```text
Logs
 ↓
Trace
 ↓
Operation
 ↓
Item
 ↓
Sync Job
```

---

## Telemetry Pillars

### Logs

Answer:

```text
What happened?
```

### Metrics

Answer:

```text
How much?
```

### Traces

Answer:

```text
How did it happen?
```

---

## Logging Strategy

### Structured Logging Required

Every important log event should include:

```text
OperationId
ItemId
SyncJobId
AccountId
RemoteItemId
```

where available.

---

### Log Levels

```text
Trace
Debug
Information
Warning
Error
Critical
```

---

### Information Examples

```text
Sync Started
Operation Completed
Delta Processing Finished
```

### Warning Examples

```text
Retry Scheduled
Temporary File Lock
Rate Limit Encountered
```

### Error Examples

```text
Operation Failed
Authentication Expired
Delta Token Invalid
```

---

## Metrics Strategy

### Queue Metrics

```text
Pending Operations
Running Operations
Completed Operations
Failed Operations
Dead Letter Count
```

### Sync Metrics

```text
Active Sync Jobs
Items Indexed
Items Synchronised
Open Conflicts
Conflict Resolution Count
```

### Provider Metrics

```text
Delta Requests
Upload Requests
Download Requests
Provider Failures
Rate Limit Events
```

### Performance Metrics

```text
Average Reconciliation Time
Average Operation Time
Queue Latency
Delta Processing Duration
Initial Crawl Duration
```

---

## Activity Progress Reporting

### Long Running Work Must Report Progress

Examples:

```text
1,500 / 250,000 Items Indexed
43,282 / 91,500 Changes Processed
2.1 GB / 5.4 GB Downloaded
```

---

### The Daemon Is The Authority

The UI must never calculate progress.

Bad:

```text
UI guesses progress
```

Good:

```text
Daemon reports progress
UI displays progress
```

---

### Progress Events Are Throttled

Avoid:

```text
1
2
3
4
...
```

Prefer:

```text
500
1000
1500
2000
```

or a time-based update interval.

---

### User Progress Principle

If AStarDev.CloudSync is performing work that may take longer than a few seconds, the user should be able to see evidence of forward progress.

---

## Distributed Tracing

### Trace Boundaries

A trace should follow work through the entire system.

```text
Provider Request
        ↓
Provider
        ↓
Reconciliation
        ↓
Operation Planning
        ↓
Queue
        ↓
Executor
        ↓
Snapshot Commit
```

---

### Trace Structure

One logical operation should produce:

```text
One Trace
Multiple Spans
```

---

## OpenTelemetry Strategy

### OpenTelemetry Is The Standard

AStarDev.CloudSync exposes:

```text
Logs
Metrics
Traces
```

through OpenTelemetry.

---

### Instrumentation Locations

Instrument:

```text
SyncEngine
Daemon
Persistence
IPC
OneDrive
```

Avoid instrumenting Domain.

---

### Domain Remains Pure

Domain types should not depend on:

```text
ILogger
ActivitySource
Telemetry APIs
```

---

## Aspire Integration

### AppHost Owns Observability Composition

Responsibilities:

```text
Telemetry Wiring
Dashboard Hosting
Development Diagnostics
Local Observability
```

---

### Aspire Is Not A Runtime Dependency

AStarDev.CloudSync must continue functioning if:

```text
AppHost
Aspire
```

are removed and the daemon is launched directly.

---

## Operational Dashboards

### Synchronisation Overview

```text
Active Jobs
Pending Operations
Open Conflicts
Provider Status
```

### Queue Dashboard

```text
Pending
Running
Failed
Dead Letter
```

### Provider Dashboard

```text
Requests
Failures
Retries
Rate Limits
```

### Crawl Dashboard

```text
Items Indexed
Items Remaining
Progress Percentage
Current Scope
```

---

## User Trust Indicators

Users should always be able to answer:

```text
Is AStarDev.CloudSync healthy?
Is AStarDev.CloudSync making progress?
Does AStarDev.CloudSync need me?
Is my data safe?
```

without reading logs.

---

## Architecture Review Checklist

```text
□ Every long-running workflow exposes progress

□ Structured logs include correlation identifiers

□ OpenTelemetry semantic conventions are used where available

□ Custom attributes are prefixed consistently

□ Domain remains telemetry-free

□ Important state transitions emit telemetry

□ Progress reporting originates from the daemon

□ Traces span the full workflow boundary

□ Metrics support operational health monitoring
```

---

## Architectural Summary

```text
Logs
    answer: What happened?

Metrics
    answer: How much?

Traces
    answer: How did it happen?
```

AStarDev.CloudSync must never reach a state where:

```text
The system knows what it is doing

but

The user cannot see evidence of progress.
```

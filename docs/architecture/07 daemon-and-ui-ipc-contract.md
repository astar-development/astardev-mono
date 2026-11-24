# 7 Daemon and UI IPC Contract

## Architectural Principles

### The Daemon Is The System Of Record

The daemon owns:

- Synchronisation state
- Reconciliation
- Operation planning
- Operation execution
- Provider communication
- Database access

The UI owns:

- Presentation
- User interaction
- Preferences
- Notifications

The UI must never access persistence directly.

---

### IPC Is The Only Integration Point

All communication between the UI and daemon occurs through the IPC contract.

Forbidden:

```text
UI -> SQLite
UI -> EF Core
UI -> Provider APIs
```

Allowed:

```text
UI
 ↓
IPC
 ↓
Daemon
```

---

### Command Query Event Separation

IPC messages are grouped into:

```text
Commands
Queries
Events
```

Each has a single responsibility.

---

## Transport

### Linux

Preferred:

```text
Unix Domain Socket
```

---

### Windows

Preferred:

```text
Named Pipe
```

---

### Abstraction

The transport implementation is hidden behind:

```csharp
IIpcServer
IIpcClient
```

Business logic must not depend on a specific transport.

---

## Commands

Commands request state changes.

Commands do not return current state.

### Examples

```csharp
ConnectAccount
DisconnectAccount
CreateSyncJob
DeleteSyncJob
AddSyncScope
RemoveSyncScope
StartSync
StopSync
PauseSync
ResumeSync
ResolveConflict
RetryOperation
SetSetting
```

---

### Example Command

```csharp
public sealed record StartSyncCommand(
    SyncJobId JobId);
```

Response:

```text
Accepted
Rejected
ValidationError
```

---

## Queries

Queries obtain state.

Queries do not mutate state.

### Examples

```csharp
GetAccounts
GetSyncJobs
GetSettings
GetSyncScopes
GetConflicts
GetOperations
GetStatistics
GetDaemonStatus
```

---

### Example Query

```csharp
public sealed record GetSyncJobQuery(
    SyncJobId JobId);
```

Result:

```csharp
public sealed record SyncJobView(...);
```

---

## Events

Events are emitted by the daemon.

The UI subscribes.

Events represent facts.

---

### Examples

```csharp
SyncStarted
SyncPaused
SyncCompleted
ConflictDetected
OperationQueued
OperationCompleted
OperationFailed
AccountConnected
AccountDisconnected
```

---

## Implemented Messages

The examples above show the shape. This is what the daemon serves today (`AStarDev.CloudSync.IPC`). A request the daemon cannot answer gets the failure reply described under Failure Handling.

### Commands

Answered with a `CommandResult` (`Accepted`, `Rejected` or `ValidationError`, an optional message for the user and, when the daemon had one, the `TraceId` of the request).

```text
ConnectAccount, DisconnectAccount
CreateSyncJob, AddSyncScope, RemoveSyncScope
StartSync, StopSync
StopDaemon
ResolveConflict, RetryOperation, IgnoreOperation
SetSetting
```

### Queries

```text
Accounts and sync:   GetAccounts, GetSyncJobs, GetSyncScopes, GetRemoteRootSummary, GetRemoteFolders, GetDriveInfo
Status and activity: GetDaemonStatus, GetSyncStatus, GetDashboardSnapshot, GetActivities, GetQueue
Conflicts:           GetConflicts, GetDeadLetters
History and audit:   GetHistory, GetAudit, ExportHistory, ExportAudit
Providers:           GetProviders
Settings:            GetSettings
Telemetry:           GetLogs, GetTraces, GetMetrics, GetDiagnostics
```

- `GetSettings` returns every known setting with its saved value or its default; `SetSetting` refuses an unknown name or a value the setting does not allow.
- `ExportHistory` and `ExportAudit` take the same filter as `GetHistory` and `GetAudit` and return `ExportView(Csv)`: RFC 4180 with a header row, and a leading `'` on any cell that starts with `=`, `+`, `-` or `@`.
- `GetAudit` first prunes entries older than the `retention.audit-days` setting (default 90).
- `GetLogs`, `GetTraces` and `GetMetrics` read the separate `telemetry.db` (kept `retention.telemetry-days`, default 7). Logs and spans are stored with e-mail addresses and paths replaced; metric tags are not stored. `GetDiagnostics` returns only the name and status of each health check, never its description.
- `GetActivities` and `GetDashboardSnapshot` share one job-level rate and time-left, worked out in the daemon over a 60 second window; both are empty while the job waits to retry.
- `GetQueue` lists pending and running operations with a count per status; `Failed` includes dead-lettered operations.

### Events

Published to every subscriber.

```text
AccountConnected, AccountDisconnected
ConflictDetected, ConflictResolved
CrawlProgressChanged (phase, items done and total)
ActivityChanged
DaemonStatusChanged
SyncStatusChanged   (published only when the status differs from the last one)
DashboardChanged
QueueChanged        (the daemon polls the queue every 2 seconds and publishes on change)
```

A client reads the current state with a query when a screen opens, then follows the events.

---

## Event Streaming

The UI establishes a long-lived subscription.

```text
UI Connected
      ↓
Subscribe
      ↓
Receive Events
      ↓
Update View Models
```

Polling should be avoided where possible.

---

## Projection Models

IPC messages must not expose domain models directly.

Domain:

```csharp
SyncItem
Conflict
Operation
```

Remain internal.

The UI receives projection models.

Example:

```csharp
SyncJobView
ConflictView
OperationView
```

---

## Progress Reporting

Progress is delivered via events.

Example:

```csharp
public sealed record CrawlProgressChanged(
    SyncJobId JobId,
    long IndexedItems,
    long TotalItems);
```

---

## Conflict Workflow

### Daemon

```text
Conflict Detected
      ↓
Persist Conflict
      ↓
Journal Conflict
      ↓
Publish ConflictDetected
```

### UI

```text
Connect
      ↓
GetConflicts(SyncJobId)
      ↓
Follow ConflictDetected / ConflictResolved
      ↓
Collect User Decision
      ↓
Send ResolveConflict Command
```

---

### Conflict Contract

Query:

```csharp
public sealed record GetConflicts(Guid SyncJobId) : IQuery<ConflictsView>;

public sealed record ConflictsView(IReadOnlyList<ConflictView> Conflicts);
```

`ConflictsView` lists the job's open and deferred conflicts, oldest first. An unknown job lists none. A conflict that cannot be read fails the query with `HandlerFailed` and a message fit for display.

Projection:

```csharp
public sealed record ConflictView(
    Guid ConflictId, Guid SyncJobId, Guid ItemId, string Type,
    DateTimeOffset DetectedUtc, string Status, string Path,
    ConflictSideView? Local, ConflictSideView? Remote);

public sealed record ConflictSideView(string Path, DateTimeOffset ModifiedUtc, long SizeBytes);
```

- `Status` is `Open` or `Deferred`. A deferred conflict is still waiting and can be resolved.
- `Path` is relative to the synchronised root: remote path, else local path, else last reconciled path.
- `Local` and `Remote` are `null` when that side has no file, for example after a delete.

Command:

```csharp
public sealed record ResolveConflict(IReadOnlyList<Guid> ConflictIds, ConflictChoice Choice) : ICommand;

public enum ConflictChoice { KeepLocal, KeepRemote, KeepBoth, Defer }
```

One id is a single resolve; several are a bulk resolve with one choice. The daemon queues the work and journals every decision.

Response:

```text
Accepted         every conflict handled
Rejected         at least one not handled; the others are still resolved
ValidationError  no id given
```

Events:

```csharp
public sealed record ConflictDetected(ConflictView Conflict) : IEvent;

public sealed record ConflictResolved(
    Guid ConflictId, Guid SyncJobId, Guid ItemId, string Resolution, DateTimeOffset ResolvedUtc) : IEvent;
```

- `ConflictDetected` is published once per new open conflict.
- `ConflictResolved` is published for each resolved conflict, which then leaves `GetConflicts`.
- `Defer` publishes no event. The conflict stays listed with status `Deferred`.
- Events reach only subscribed clients. A client that connects later learns of conflicts through `GetConflicts`.

---

## Connection Lifecycle

### Daemon Start

```text
Start IPC Server
      ↓
Accept Connections
```

---

### UI Start

```text
Connect
      ↓
Query Initial State
      ↓
Subscribe To Events
```

---

### UI Disconnect

```text
Disconnect
```

Synchronisation continues.

---

## Security Principles

All IPC endpoints are local-machine only.

The initial design assumes:

```text
Single User
Single Machine
```

No remote access is exposed.

---

## Failure Handling

If the daemon becomes unavailable:

```text
UI
 ↓
Show Disconnected State
 ↓
Retry Connection
```

The UI must degrade gracefully.

### Failure Reply

A request the daemon cannot answer gets an error reply with the request id, a `message` fit for display and a `code`. The code reads `CS-IPC-NNNN` and names the `IpcError` case the daemon hit; its help link is `https://astar-development.co.uk/errors/<code>`. The client surfaces the reply as `HandlerFailed` with the code in `Reported`; a code it cannot read is dropped and the failure still arrives.

---

## Architectural Summary

The daemon owns behaviour.

The UI owns presentation.

Commands change state.
Queries read state.
Events report state changes.

The IPC contract is the only boundary between the two processes.

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

---

## Architectural Summary

The daemon owns behaviour.

The UI owns presentation.

Commands change state.
Queries read state.
Events report state changes.

The IPC contract is the only boundary between the two processes.

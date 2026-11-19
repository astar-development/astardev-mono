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
Publish Event
```

### UI

```text
Receive Event
      ↓
Display Conflict
      ↓
Collect User Decision
      ↓
Send ResolveConflict Command
```

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

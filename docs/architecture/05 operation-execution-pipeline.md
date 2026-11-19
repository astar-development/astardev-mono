# 5 Operation Execution Pipeline

## Architectural Principles

### Operations Are Durable

An operation must survive:

- Daemon restart
- Machine reboot
- Process crash
- Power failure

Operations are always persisted before execution begins.

```text
Operation
    ↓
Persist
    ↓
Execute
```

Never:

```text
Execute
    ↓
Persist
```

---

### Operations Are Idempotent

Every operation must be safely re-runnable.

Example:

```text
Upload succeeds
↓
Daemon crashes
↓
Operation retried
```

Retrying must not corrupt sync state.

---

### Executors Are Dumb

Executors perform work.

Executors do not make sync decisions.

Bad:

```text
Executor
    ↓
Should I upload?
```

Good:

```text
Executor
    ↓
Upload File X
```

All decisions occur earlier in the pipeline.

---

### The Operation Queue Is The Heart Of The Daemon

The queue provides:

- Durability
- Retry handling
- Recovery
- Observability
- Rate limiting
- Prioritisation
- Concurrency control

The queue is the primary architectural component of the daemon.

---

## High-Level Pipeline

```text
Filesystem / Graph Changes
            ↓
Observation
            ↓
Canonical State Builder
            ↓
Reconciler
            ↓
Sync Decision
            ↓
Operation Planner
            ↓
Persist Operations
            ↓
Scheduler
            ↓
Executors
            ↓
Operation Result
            ↓
Sync Engine
            ↓
Snapshot Update
            ↓
Event Journal
```

---

## Stage 1 - Observation

Sources:

```text
Filesystem Watcher
Filesystem Scanner
Graph Delta Feed
```

Produces:

```text
ObservedChange
```

Examples:

```text
FileCreated
FileModified
FileDeleted
FileRenamed
```

---

## Stage 2 - Canonical State Builder

Builds:

```text
CanonicalItemState
```

From:

```text
KnownSnapshot
LocalSnapshot
RemoteSnapshot
```

---

## Stage 3 - Reconciliation

Input:

```text
CanonicalItemState
```

Output:

```text
SyncDecision
```

Examples:

```text
NoOp
UpdateRemote
RenameLocal
Conflict
```

No execution occurs here.

---

## Stage 4 - Operation Planning

A decision is transformed into executable operations.

Example:

```text
UpdateRemote
```

may produce:

```text
ReadFile
UploadFile
VerifyUpload
CommitSnapshot
```

One decision may generate multiple operations.

---

## Stage 5 - Queue Persistence

Operations are stored before execution.

Example:

| Id | Status |
|----|--------|
| 101 | Pending |
| 102 | Pending |
| 103 | Pending |

After persistence they become eligible for scheduling.

---

## Stage 6 - Scheduling

The scheduler selects executable work.

Rules:

- Do not process conflicting operations concurrently.
- Do not process multiple active operations for the same item.
- Respect concurrency limits.
- Respect provider throttling limits.

---

## Operation State Machine

```csharp
public enum OperationStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    DeadLetter
}
```

### Success Path

```text
Pending
    ↓
Running
    ↓
Completed
```

### Retry Path

```text
Pending
    ↓
Running
    ↓
Failed
    ↓
Pending
```

### Terminal Failure Path

```text
Pending
    ↓
Running
    ↓
Failed
    ↓
DeadLetter
```

---

## Dead Letter Queue

One problematic file must never block overall synchronisation.

Examples:

```text
AccessDenied
CorruptFile
PermanentProviderFailure
RepeatedExecutionFailure
```

After a configurable retry threshold:

```text
DeadLetter
```

The system continues processing all other work.

---

## Operation Priorities

```csharp
public enum OperationPriority
{
    UserInitiated,
    Interactive,
    Background,
    Maintenance
}
```

### UserInitiated

Examples:

```text
Manual Sync
Conflict Resolution
```

Highest priority.

### Background

Examples:

```text
Normal Uploads
Normal Downloads
```

Default priority.

### Maintenance

Examples:

```text
Snapshot Pruning
Database Cleanup
```

Lowest priority.

---

## Executors

Examples:

```text
IOneDriveExecutor
ILocalFileExecutor
```

Operation types:

```text
UploadFileOperation
DownloadFileOperation
RenameRemoteOperation
RenameLocalOperation
DeleteRemoteOperation
DeleteLocalOperation
```

Executors only perform work against their environment.

---

## Snapshot Commit Stage

Executors do not update sync state directly.

Bad:

```text
Executor
    ↓
Update Snapshot
```

Good:

```text
Executor
    ↓
Operation Result
    ↓
Sync Engine
    ↓
Update Snapshot
```

This preserves the rule:

> Only the Sync Engine can mutate sync state.

---

## Crash Recovery

Example:

```text
Operation 123
Status = Running
```

Daemon restart:

```text
Find Running Operations
    ↓
Mark Failed
    ↓
Increment Attempt Count
    ↓
Requeue If Eligible
```

No special-case recovery logic is required.

---

## Concurrency Rules

Per-item locking is required.

Bad:

```text
Upload report.docx
Rename report.docx
```

Running simultaneously.

Good:

```text
Lock Item
    ↓
Execute Operation
    ↓
Unlock Item
```

Different items may execute concurrently.

The same logical item may not.

---

## Summary

The operation queue is the system's central nervous system.

All reconciliation outcomes eventually become durable operations.

Once represented as operations, the daemon automatically gains:

- Retry behaviour
- Crash recovery
- Progress reporting
- Observability
- Prioritisation
- Concurrency control
- Throttling control

The Sync Engine decides.
The Operation Queue coordinates.
The Executors perform.

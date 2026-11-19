# Sync State Model

## Design Principles

AStarDev.CloudSync optimises for correctness, recoverability and user trust before performance, convenience or automation.
If AStarDev.CloudSync is doing work that may take longer than a few seconds, the user should be able to see evidence of forward progress.

### Domain and Persistence are Independent

The domain model must have no dependency on:

- EF Core
- SQLite
- Migrations
- Persistence concerns

Persistence is an implementation detail.

```text
Domain
    ↓
Persistence Abstractions
    ↓
EF Core / SQLite
```

### Strongly Typed Domain

Use domain-specific value objects for identities and important concepts.

```csharp
public readonly record struct ItemId(Guid Value);
public readonly record struct SyncJobId(Guid Value);
public readonly record struct AccountId(Guid Value);
public readonly record struct RemoteItemId(string Value);
public readonly record struct DeltaToken(string Value);
public readonly record struct ETag(string Value);
public readonly record struct RelativePath(string Value);
```

Avoid wrapping every primitive.

Strong types are reserved for:

- identities
- provider identifiers
- sync-critical values

### Immutable Domain

Domain state should be represented using immutable records.

```csharp
public sealed record SyncItem(
    ItemId Id,
    SyncJobId SyncJobId,
    RemoteItemId RemoteItemId,
    RelativePath Path);
```

State transitions create new state.

### Sync Engine Owns State Mutation

Only the Sync Engine can mutate sync state.

```text
UI
    ↓
Commands
    ↓
Sync Engine
    ↓
Persistence
```

Forbidden:

```text
UI -> DbContext
Provider -> DbContext
```

All changes flow through the engine.

## Core Domain Concepts

### Account

Represents a connected cloud account.

Examples:

- Personal OneDrive
- Business OneDrive

### Sync Job

Represents a configured sync relationship.

```text
OneDrive
    ↔
/home/jason/OneDrive
```

### Item

Represents a logical file or folder.

Identity is independent of path.

Paths may change.

Item identity does not.

### Snapshot

Represents observed state at a point in time.

Examples:

- Size
- ModifiedUtc
- ETag
- Hash

A single Item may have many Snapshots over time.

### Operation

Represents work to be performed.

Examples:

- Upload
- Download
- DeleteLocal
- DeleteRemote
- RenameLocal
- RenameRemote

The engine produces Operations.

Executors perform Operations.

### Conflict

Represents an unresolved sync disagreement.

Examples:

- Local modified vs Remote modified
- Delete vs Modify
- Rename vs Delete

Conflicts remain until resolved.

### Event

Represents a significant occurrence.

Examples:

- FileDetected
- ConflictDetected
- OperationQueued
- OperationCompleted

Used primarily for diagnostics and supportability.

## Suggested SQLite Schema

### Accounts

| Column | Notes |
|----------|----------|
| Id | PK |
| ProviderType | OneDrive, etc |
| DisplayName | User-facing account name |
| AccountIdentifier | Provider account ID |
| CreatedUtc | Created timestamp |

### SyncJobs

| Column | Notes |
|----------|----------|
| Id | PK |
| AccountId | FK |
| Name | Friendly name |
| LocalRootPath | Local sync root |
| RemoteRootId | Remote root identifier |
| Enabled | Sync enabled flag |
| CreatedUtc | Created timestamp |
| UpdatedUtc | Last updated timestamp |

### Items

| Column | Notes |
|----------|----------|
| Id | PK |
| SyncJobId | FK |
| ItemType | File/Folder |
| ParentItemId | Optional FK |
| RemoteItemId | OneDrive item ID |
| LocalPath | Current local path |
| CreatedUtc | Created timestamp |
| UpdatedUtc | Last updated timestamp |

Acts as the identity bridge between:

```text
Database
↔
Filesystem
↔
OneDrive
```

### ItemSnapshots

| Column | Notes |
|----------|----------|
| Id | PK |
| ItemId | FK |
| Size | Observed size |
| ModifiedUtc | Last modified |
| Hash | Optional |
| ETag | Provider ETag |
| CTag | Provider CTag |
| ObservedUtc | Observation time |
| SnapshotSource | Local or Remote |

### ProviderState

| Column | Notes |
|----------|----------|
| SyncJobId | FK |
| DeltaToken | Graph delta token |
| LastSyncUtc | Last successful sync |
| LastScanUtc | Last scan time |

### Operations

| Column | Notes |
|----------|----------|
| Id | PK |
| ItemId | FK |
| OperationType | Upload, Download, etc |
| Status | Pending, Running, Completed, Failed |
| Attempts | Retry count |
| QueuedUtc | Queue timestamp |
| StartedUtc | Execution start |
| CompletedUtc | Execution end |
| LastError | Most recent error |

### Conflicts

| Column | Notes |
|----------|----------|
| Id | PK |
| ItemId | FK |
| ConflictType | Conflict category |
| LocalSnapshotId | FK |
| RemoteSnapshotId | FK |
| Status | Open, Resolved, Ignored |
| CreatedUtc | Created timestamp |
| ResolvedUtc | Resolution timestamp |

### EventJournal

| Column | Notes |
|----------|----------|
| Id | PK |
| OccurredUtc | Event time |
| EventType | Domain event |
| AggregateId | Related aggregate |
| PayloadJson | Diagnostic payload |

## Architectural Flow

```text
Filesystem Watcher
        ↓
Provider Deltas
        ↓
Change Detection
        ↓
Sync Engine
        ↓
Reconciliation
        ↓
Operations Queue
        ↓
Executors
        ↓
Filesystem / Graph API
```

The database remains the system of record for sync state.

## Future Design Topics

1. Reconciliation algorithm
2. Rename detection
3. Conflict resolution rules
4. Delta-token lifecycle
5. Operation retry strategy
6. Daemon/UI IPC contract
7. Multi-provider abstraction

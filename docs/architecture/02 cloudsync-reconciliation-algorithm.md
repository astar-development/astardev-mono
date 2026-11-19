# Reconciliation Algorithm

## Design Principles

### Rename Is a First-Class Operation

Rename and move operations are first-class state transitions.

They must not be modelled as delete/create operations unless detection fails.

```text
Rename != Delete + Create
```

File identity remains stable while location changes.

---

### No Silent Data Loss

AStarDev.CloudSync must never silently discard user data.

When reconciliation cannot determine a single safe outcome, it must produce a conflict.

```text
Ambiguity
    =
Conflict

Never
    =
Guess
```

---

### Known State Is Mandatory

The reconciler reasons from:

```text
Known State
    +
Current Local State
    +
Current Remote State
```

It must never attempt to infer intent from Local and Remote alone.

---

## Canonical Item State

```csharp
public sealed record CanonicalItemState(
    ItemId ItemId,
    KnownSnapshot? Known,
    LocalSnapshot? Local,
    RemoteSnapshot? Remote);
```

### Known Snapshot

Represents the last successfully reconciled state.

```csharp
public sealed record KnownSnapshot(
    RelativePath Path,
    long Size,
    DateTimeOffset ModifiedUtc,
    ETag? ETag,
    ContentFingerprint? Fingerprint);
```

### Local Snapshot

Represents the current filesystem observation.

```csharp
public sealed record LocalSnapshot(
    RelativePath Path,
    long Size,
    DateTimeOffset ModifiedUtc,
    LocalIdentity? Identity,
    ContentFingerprint? Fingerprint);
```

### Remote Snapshot

Represents the current OneDrive observation.

```csharp
public sealed record RemoteSnapshot(
    RemoteItemId ItemId,
    RelativePath Path,
    long Size,
    DateTimeOffset ModifiedUtc,
    ETag ETag,
    CTag? CTag);
```

---

## State Components

Every item consists of three independent aspects.

### Identity

Never changes.

Examples:

```text
ItemId
RemoteItemId
```

### Location

May change.

Examples:

```text
RelativePath
Parent Folder
File Name
```

### Content

May change independently of location.

Examples:

```text
Size
Fingerprint
ModifiedUtc
ETag
```

---

## Reconciler Contract

```csharp
public interface IReconciler
{
    SyncDecision Reconcile(CanonicalItemState state);
}
```

---

## Decision Types

```csharp
public enum SyncDecisionType
{
    NoOp,

    CreateLocal,
    CreateRemote,

    UpdateLocal,
    UpdateRemote,

    RenameLocal,
    RenameRemote,

    DeleteLocal,
    DeleteRemote,

    Conflict
}
```

---

## Fundamental Rules

### Rule 1 - No Changes

```text
Known  = A
Local  = A
Remote = A
```

Decision:

```text
NoOp
```

---

### Rule 2 - Local Create

```text
Known  = Missing
Local  = Exists
Remote = Missing
```

Decision:

```text
CreateRemote
```

---

### Rule 3 - Remote Create

```text
Known  = Missing
Local  = Missing
Remote = Exists
```

Decision:

```text
CreateLocal
```

---

### Rule 4 - Local Content Change

```text
Known  = A
Local  = B
Remote = A
```

Decision:

```text
UpdateRemote
```

---

### Rule 5 - Remote Content Change

```text
Known  = A
Local  = A
Remote = B
```

Decision:

```text
UpdateLocal
```

---

### Rule 6 - Local Rename

```text
Known:
    /Reports/Q1.xlsx

Local:
    /Archive/Q1.xlsx

Remote:
    /Reports/Q1.xlsx
```

Decision:

```text
RenameRemote
```

---

### Rule 7 - Remote Rename

```text
Known:
    /Reports/Q1.xlsx

Local:
    /Reports/Q1.xlsx

Remote:
    /Archive/Q1.xlsx
```

Decision:

```text
RenameLocal
```

---

### Rule 8 - Local Delete

```text
Known  = Exists
Local  = Missing
Remote = Exists
```

Decision:

```text
DeleteRemote
```

---

### Rule 9 - Remote Delete

```text
Known  = Exists
Local  = Exists
Remote = Missing
```

Decision:

```text
DeleteLocal
```

---

### Rule 10 - Concurrent Modification

```text
Known  = A
Local  = B
Remote = C

Where:

B != A
C != A
```

Decision:

```text
Conflict
```

---

## Orthogonal Changes

Location changes and content changes are treated as independent concerns.

Example:

```text
Known
    /Reports/Q1.xlsx

Local
    /Archive/Q1.xlsx

Remote
    /Reports/Q1.xlsx (modified)
```

This is not considered a conflict.

The engine should safely merge the changes:

```text
Rename
+
Content Update
```

Only competing modifications to the same concern produce conflicts.

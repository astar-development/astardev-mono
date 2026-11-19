# 6 Initial Crawl and Delta Synchronisation

## Architectural Principles

### Selective Sync Is A Core Feature

AStarDev.CloudSync does not assume an entire OneDrive should be synchronised.

Users may select:

- Individual folders
- Nested folders
- Multiple independent folders
- Individual files at the drive root

Example:

```text
OneDrive
├── Photos                ✗
├── Videos                ✗
├── Documents             ✓
│   ├── Clients           ✓
│   └── Archive           ✗
└── Notes                 ✓
```

Only selected content becomes part of the sync scope.

---

### Nothing Is Synced By Default

When a new account is connected:

```text
Connected
    !=
Synchronising
```

The initial state is:

```text
Account Connected
No Sync Scope Defined
```

Users explicitly choose what should be synchronised.

---

### Sync Scope Is A First-Class Concept

A Sync Job may contain one or more Sync Scopes.

```text
Sync Job
    ├── Documents
    ├── Notes
    └── Projects
```

The engine only indexes and synchronises selected scopes.

---

### Delta Synchronisation Is The Normal Operating Mode

Full crawls are exceptional.

Normal operation is:

```text
Delta Token
    ↓
Changed Items
    ↓
Reconciliation
```

The objective is to move into delta mode as quickly as possible.

---

### Initial Crawl Must Be Resumable

A crawl of hundreds of thousands of files may take significant time.

AStarDev.CloudSync must persist progress continuously.

A restart should resume from the last checkpoint.

Never:

```text
Restart
    ↓
Start Again From Zero
```

---

## Sync Scope Discovery

After account connection:

```text
Authenticate
    ↓
Enumerate OneDrive Hierarchy
    ↓
Present Tree To User
    ↓
Select Sync Scopes
    ↓
Create Sync Job
```

Only after scope selection does indexing begin.

---

## Bootstrap Modes

When a scope is selected the user chooses an initialisation strategy.

### Download From OneDrive

```text
OneDrive
    ↓
Local
```

Remote content is considered authoritative.

---

### Upload To OneDrive

```text
Local
    ↓
OneDrive
```

Local content is considered authoritative.

---

### Merge Existing Content

```text
Local
    ↔
OneDrive
```

Existing content on both sides is analysed using normal reconciliation rules.

No content is discarded without an explicit user decision.

---

## Initial Crawl Pipeline

```text
Selected Scope
        ↓
Enumerate Remote Items
        ↓
Persist Item Metadata
        ↓
Persist Snapshots
        ↓
Persist Crawl Checkpoint
        ↓
Continue
```

Metadata is stored incrementally.

Large crawls must not accumulate state purely in memory.

---

## Incremental Availability

AStarDev.CloudSync becomes usable before crawl completion.

Example:

```text
Items Indexed
143,821 / 702,144

Status
Synchronising
```

The application remains operational while indexing continues.

---

## Metadata First Strategy

For MVP:

```text
Metadata
    YES

Files On-Demand
    FUTURE
```

The crawl records:

- Item identity
- Path
- Size
- Modified time
- ETag
- Parent relationship

The system does not require a complete content download before becoming operational.

---

## Provider State

Provider state is persisted continuously.

Example:

```text
Delta Token
Last Successful Delta
Last Crawl Checkpoint
Last Crawl Time
```

This state enables efficient recovery and resumption.

---

## Delta Token Lifecycle

### Initial Crawl

```text
Enumerate Scope
    ↓
Reach Delta Boundary
    ↓
Store Delta Token
```

---

### Normal Operation

```text
Stored Delta Token
    ↓
Request Deltas
    ↓
Apply Changes
    ↓
Store New Delta Token
```

---

### Token Failure

If a delta token becomes invalid:

```text
Invalidate Token
    ↓
Scoped Reindex
    ↓
Acquire New Token
    ↓
Resume Delta Mode
```

A token failure must never require a complete application reset.

---

## Recovery Rules

### Daemon Restart

```text
Load Provider State
    ↓
Resume Crawl Or Delta Processing
```

---

### Partial Crawl Failure

```text
Load Last Checkpoint
    ↓
Continue Enumeration
```

---

### Network Failure

```text
Pause Processing
    ↓
Retry
    ↓
Resume
```

No special reconciliation logic is required.

---

## Future Capability: Files On-Demand

Files On-Demand is explicitly considered a post-MVP capability.

Future implementations may introduce:

```text
Online Only
Cached
Always Available
```

materialisation states.

The MVP architecture must not prevent this capability from being added later.

---

## Summary

The objective of the initial crawl is not to synchronise content.

The objective is to establish authoritative knowledge of the selected sync scope and transition into efficient delta-based synchronisation.

For large OneDrive accounts:

```text
Initial Crawl
    ↓
Checkpointed Inventory
    ↓
Delta Mode Forever
```

is the desired operational model.

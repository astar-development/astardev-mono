# Architecture Index

## Purpose

This document is the entry point for the AStarDev.CloudSync architecture.

It provides:

- Recommended reading order
- Document purpose
- Key decisions
- Cross-document relationships
- Architectural principles

This index should be updated whenever a new architectural document is added.

---

# Core Architectural Philosophy

AStarDev.CloudSync optimises for:

```text
Correctness
    ↓
Observability
    ↓
Recoverability
    ↓
User Trust
```

before:

```text
Convenience
Performance
Automation
```

Fundamental principle:

```text
Never silently lose user data.
```

---

# Recommended Reading Order

## Foundation Layer

### 1. Sync State Model

Purpose:

```text
Defines the canonical state model.
```

Key Decisions:

- Known State is authoritative.
- Local and Remote are observations.
- Identity and path are separate concepts.

---

### 2. Reconciliation Algorithm

Purpose:

```text
Defines how state becomes decisions.
```

Key Decisions:

- Reconciliation is deterministic.
- Decisions precede execution.
- No silent conflict resolution.

---

### 3. Reconciliation Decision Table

Purpose:

```text
Defines conflict and merge outcomes.
```

Key Decisions:

- Competing intent becomes conflict.
- Orthogonal changes may be merged.

---

### 4. Rename Detection Rules

Purpose:

```text
Defines identity-based rename detection.
```

Key Decisions:

- Rename is first-class.
- False-negative rename is preferable to false-positive rename.

---

## Execution Layer

### 5. Operation Execution Pipeline

Purpose:

```text
Defines durable operation execution.
```

Key Decisions:

- Operation Queue is the heart of the daemon.
- Operations are durable.
- Executors are dumb.

---

### 6. Initial Crawl and Delta Synchronisation

Purpose:

```text
Defines onboarding and steady-state sync.
```

Key Decisions:

- Selective sync is first-class.
- Delta mode is the normal operating mode.
- Files On-Demand is post-MVP.

---

### 7. Daemon and UI IPC Contract

Purpose:

```text
Defines all daemon/UI communication.
```

Key Decisions:

- Daemon owns state.
- UI is a client.
- Commands, Queries and Events are separate concerns.

---

## User Safety Layer

### 8. Conflict Resolution UX

Purpose:

```text
Defines user-facing conflict behaviour.
```

Key Decisions:

- Conflicts are not errors.
- Data safety takes precedence.

---

### 9. Provider Abstraction and OneDrive Integration

Purpose:

```text
Defines provider boundaries.
```

Key Decisions:

- Providers expose capabilities.
- Sync Engine owns synchronisation decisions.
- RemoteItemId is a domain concept.

---

### 10. Error Handling and Recovery Strategy

Purpose:

```text
Defines failure classification and recovery.
```

Key Decisions:

- Errors are expected.
- Recovery before failure.
- One item must not stop synchronisation.

---

## Governance Layer

### 11. Solution Structure and Module Boundaries

Purpose:

```text
Defines code ownership and dependencies.
```

Key Decisions:

- Domain depends on nothing.
- Dependency direction is enforced.
- Architectural erosion should be difficult.

---

### 12. Observability and Telemetry Strategy

Purpose:

```text
Defines telemetry architecture.
```

Key Decisions:

- OpenTelemetry first.
- Semantic conventions before custom conventions.
- Progress must be observable.

---

### 13. User Trust and Activity Model

Purpose:

```text
Defines user-visible system behaviour.
```

Key Decisions:

- Every significant state has a visible representation.
- Trust is built through visibility.
- Progress is reported by the daemon.

---

# Architectural Invariants

The following statements should always remain true.

```text
Only the Sync Engine mutates sync state.
```

```text
The UI never accesses persistence directly.
```

```text
Providers do not perform reconciliation.
```

```text
The Domain depends on nothing.
```

```text
Operation Queue is the heart of the daemon.
```

```text
Paths are not identities.
```

```text
Rename is a first-class operation.
```

```text
Conflicts are preferable to silent data loss.
```

```text
If AStarDev.CloudSync is doing work, that work should be observable.
```

```text
The user should never have to guess whether progress is being made.
```

---

# Future Documents

Potential future documents:

```text
14 Solution Startup and Composition
15 Architecture Testing and Governance
16 Data Model and Persistence Design
17 Notifications and User Attention Model
18 Files On-Demand Architecture
19 Security and Secrets Management
20 Release and Upgrade Strategy
```

---

# Final Principle

When uncertain, prefer the option that best preserves:

```text
Correctness
Observability
Recoverability
Trust
```

These values are the foundation of the AStarDev.CloudSync architecture.

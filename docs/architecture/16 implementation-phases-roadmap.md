# 16 Implementation Phases Roadmap

## Purpose

This document captures the recommended transition from architecture into implementation.

The objective is to avoid premature feature development and instead establish a solid foundation that protects the architectural principles already documented.

---

### Guiding Principle

AStarDev.CloudSync has now reached a stage where additional architecture documents should primarily be driven by implementation experience.

The recommendation is:

```text
Stop Architecting
        ↓
Start Building
        ↓
Learn From Reality
        ↓
Refine Architecture
```

---

## Phase 1 - Solution Skeleton

### Goal

Create the physical solution structure exactly as defined in Document 11.

### Deliverables

```text
AStarDev.CloudSync.AppHost

AStarDev.CloudSync.Domain
AStarDev.CloudSync.SyncEngine
AStarDev.CloudSync.Persistence
AStarDev.CloudSync.OneDrive
AStarDev.CloudSync.IPC
AStarDev.CloudSync.Daemon
AStarDev.CloudSync.UI
AStarDev.CloudSync.Observability

AStarDev.CloudSync.TestsArchitecture
```

### Success Criteria

```text
Projects exist
References are correct
Solution builds
```

No business logic required.

---

## Phase 2 - Architecture Enforcement

### Goal

Protect the architecture before writing significant code.

### Deliverables

```text
AStarDev.CloudSync.TestsArchitecture
```

Using:

```text
NetArchTest.Rules
```

## Initial Rules

```text
Domain must not reference EF Core

Domain must not reference Avalonia

Domain must not reference Microsoft.Graph

UI must not reference Persistence

OneDrive must not reference Persistence

Persistence must not reference SyncEngine
```

### Success Criteria

```text
Architecture violations fail the build
```

---

## Phase 3 - Startup and Composition

### Goal

Create the runtime host structure.

### Deliverables

```text
AppHost
        ↓

Daemon
        ↓

UI
```

### Success Criteria

```text
AppHost launches
Daemon starts
UI connects via IPC
```

No OneDrive functionality required.

---

## Phase 4 - Observability First

### Goal

Introduce observability before business behaviour.

### Deliverables

```text
ILogger
ActivitySource
Meter
OpenTelemetry
```

Integrated into:

```text
Daemon
SyncEngine
Persistence
OneDrive
```

### Success Criteria

```text
Logs visible
Metrics visible
Traces visible
```

---

## Phase 5 - First Vertical Slice

### Goal

Exercise all major architectural boundaries.

## Recommended Slice

```text
Connect OneDrive
        ↓
Enumerate Root
        ↓
Return Item Count
        ↓
Display In UI
```

## Systems Exercised

```text
Provider
Daemon
IPC
UI
Telemetry
```

### Success Criteria

```text
User can connect an account
User can see remote item count
```

---

## Phase 6 - Incremental Expansion

### Suggested Order

```text
Remote Enumeration
        ↓
Selective Sync
        ↓
Initial Crawl
        ↓
Delta Processing
        ↓
Operation Queue
        ↓
Reconciliation
        ↓
Conflict Handling
```

Implement vertically.

Avoid developing entire subsystems in isolation.

---

## CI/CD Principle

Architectural compliance is a prerequisite for compilation and testing.

Pipeline order:

```text
Architecture Tests
        ↓
Build
        ↓
Unit Tests
        ↓
Integration Tests
        ↓
Packaging
        ↓
Release
```

If architecture tests fail:

```text
Stop Pipeline
```

Immediately.

---

## What Not To Do Yet

Avoid spending significant effort on:

```text
Files On-Demand

Advanced Notifications

Multi-Provider Support

Release Automation

Large-Scale Optimisation
```

until the core architecture has been proven.

---

## Future Architecture Documents

These should ideally be informed by implementation experience.

Potential candidates:

```text
Data Model and Persistence Design

Notifications and User Attention Model

Files On-Demand Architecture

Security and Secrets Management

Release and Upgrade Strategy
```

---

## Final Recommendation

The architecture is now sufficiently mature.

The greatest risk is no longer poor design.

The greatest risk is delaying implementation.

Build the skeleton.
Protect the boundaries.
Instrument everything.
Implement one vertical slice.

Let the code teach the next architectural lesson.

Fair winds and following seas. ⚓

# 11 Solution Structure and Module Boundaries

## Architectural Principles

### Modules Exist To Own Behaviour

Modules are organised around responsibilities, not implementation details.

Good:

```text
Domain
SyncEngine
Persistence
OneDrive
```

Avoid:

```text
Models
Contracts
Helpers
Utilities
Common
Shared
```

Module names should explain why they exist.

---

### Dependency Direction Is Intentional

Dependencies are allowed only in approved directions.

Convenience is never a reason to violate architectural boundaries.

```text
Easy Today
    ↓
Big Ball Of Mud Tomorrow
```

---

### Domain Sits At The Centre

The Domain is the core of the application.

Everything depends on Domain.

Domain depends on nothing.

The Domain holds identities, value objects, entities and rules. Nothing in it
performs I/O, so nothing in it returns `Result`. Contracts that cross an I/O
boundary live in `AStarDev.CloudSync.Ports`.

---

### Infrastructure Is Replaceable

The following must remain replaceable without Domain changes:

- SQLite
- OneDrive
- IPC Transport
- Logging
- Telemetry
- Aspire

---

## High-Level Solution Structure

```text
AStarDev.CloudSync.AppHost

AStarDev.CloudSync.Domain

AStarDev.CloudSync.Ports

AStarDev.CloudSync.SyncEngine

AStarDev.CloudSync.Classification

AStarDev.CloudSync.Persistence

AStarDev.CloudSync.OneDrive

AStarDev.CloudSync.IPC

AStarDev.CloudSync.Daemon

AStarDev.CloudSync.UI

AStarDev.CloudSync.Observability

AStarDev.CloudSync.Errors

AStarDev.CloudSync.FakeProvider

AStarDev.CloudSync.TestsIntegration
```

---

## AStarDev.CloudSync.AppHost

Purpose:

```text
Developer Experience
Local Orchestration
Application Composition
Diagnostics
```

Responsibilities:

- Aspire configuration
- Local orchestration
- OpenTelemetry wiring
- Service startup
- Development diagnostics

Must Not Contain:

```text
Business Rules
Sync Logic
Provider Logic
Persistence Logic
```

---

## AStarDev.CloudSync.Domain

Purpose:

```text
Business Concepts
```

Contains:

- Value Objects
- Records
- Identifiers
- Domain Events
- Enums

Examples:

```text
ItemId
RemoteItemId
SyncJob
Conflict
Operation
```

Dependencies:

```text
None
```

---

## AStarDev.CloudSync.Ports

Purpose:

```text
Boundary Contracts
```

Contains:

- Provider port (`ICloudProvider`) and provider models
- `ProviderError`
- State store interfaces implemented by Persistence

Dependencies:

```text
Domain
AStarDev.FunctionalParadigm
```

Must Not Contain:

```text
Implementations
EF Core
Graph SDK
Avalonia
Sync Decisions
```

Every method on a port can fail, so it returns a `Result` from
`AStarDev.FunctionalParadigm` and takes a `CancellationToken`.

---

## AStarDev.CloudSync.SyncEngine

Purpose:

```text
Synchronisation Behaviour
```

Contains:

- Reconciliation
- Rename Detection
- Conflict Detection
- Operation Planning
- State Transitions

Dependencies:

```text
Domain
Ports
```

Must Not Contain:

```text
EF Core
Graph SDK
Avalonia
```

---

## AStarDev.CloudSync.Classification

Purpose:

```text
File Classification
```

Contains:

- Classification engine: tokeniser, phrase matching, signals, combiner
- Suggestion scanning, seed parsing, vocabulary provider
- Reclassify logic and the classify-on-sync hook

Dependencies:

```text
Domain
Ports
Errors
AStarDev.FunctionalParadigm
```

Must Not Contain:

```text
EF Core
Avalonia
Sync Decisions
```

Only the Daemon references this module. See document 25.

---

## AStarDev.CloudSync.Persistence

Purpose:

```text
State Storage
```

Contains:

- DbContext
- Entities
- Mappings
- Migrations
- Repositories
- Telemetry store: a second SQLite file, `telemetry.db`, with its own `TelemetryDbContext` and migrations, for logs, spans and metric samples. It lives in Persistence (it needs only Ports and EF Core, so no boundary reason to split it out) and is kept apart from the sync database so telemetry volume and failures never touch sync state

Dependencies:

```text
Domain
Ports
```

Must Not Contain:

```text
Sync Logic
Provider Logic
UI Logic
```

---

## AStarDev.CloudSync.OneDrive

Purpose:

```text
OneDrive Integration
```

Contains:

- OAuth
- Graph Integration
- Uploads
- Downloads
- Delta Queries

Dependencies:

```text
Domain
Ports
```

Must Not Contain:

```text
Conflict Resolution
Reconciliation
Operation Planning
```

---

## AStarDev.CloudSync.IPC

Purpose:

```text
Daemon / UI Communication
```

Contains:

- Commands
- Queries
- Events
- Transport Abstractions
- Projection Models

Dependencies:

```text
Domain
```

Must Not Depend On:

```text
UI
Daemon
Persistence
```

---

## AStarDev.CloudSync.Daemon

Purpose:

```text
Application Runtime
```

Contains:

- Hosting
- Schedulers
- Background Services
- Composition Root

Dependencies:

```text
Domain
Ports
SyncEngine
Persistence
OneDrive
IPC
Observability
```

---

## AStarDev.CloudSync.UI

Purpose:

```text
User Experience
```

Contains:

- Avalonia Views
- View Models
- IPC Client
- User Interaction

Must Not Contain:

```text
Database Access
Sync Logic
Provider Logic
```

---

## AStarDev.CloudSync.Observability

Purpose:

```text
Logging
Metrics
Tracing
Diagnostics
```

Contains:

- Serilog
- OpenTelemetry
- Instrumentation
- Dashboard Configuration

Must Never Become:

```text
Common
Shared
Utilities
```

---

## AStarDev.CloudSync.FakeProvider

Purpose:

```text
Run The Sync Engine Without OneDrive
```

Contains:

- FakeCloudProvider (scripted and generated trees, recorded uploads, renames and deletes)

Rules:

- Depends only on Domain and Ports
- Exposes no public types; the daemon and the test projects see it through InternalsVisibleTo
- The daemon references it in Debug builds only, so a release build never ships it
- A Debug daemon selects it with `CloudSync:Provider=Fake`; see `docs/runbooks/cloudsync-fake-provider.md`

---

## AStarDev.CloudSync.Errors

Purpose:

```text
One Error Code And Help Link Contract
```

Contains:

- ErrorCode (`CS-<MODULE>-<NNNN>`, validated)
- IErrorInfo (code and help link of an error)
- ErrorLinks (link root `https://astar-development.co.uk/errors/`, link for a code, `Exception.HelpLink` helper)

Rules:

- Depends only on `AStarDev.FunctionalParadigm`
- Ports, IPC, Persistence, SyncEngine, OneDrive, Daemon and UI may reference it; Domain may not
- Holds no module's error types: each module keeps its own and implements `IErrorInfo`

Must Never Become:

```text
Common
Shared
Utilities
```

---

## AStarDev.CloudSync.TestsIntegration

Purpose:

```text
System Verification
```

Contains:

- Integration Tests
- Recovery Tests
- Scale Tests

Examples:

```text
700k Item Tests
Crash Recovery Tests
Conflict Tests
Rename Tests
```

---

## Dependency Rules

### AStarDev.FunctionalParadigm

`AStarDev.FunctionalParadigm` is a dependency-free utility library
(`Result`, `Unit`, `Try`). Any module except Domain may reference it, because
any method that can fail returns a `Result`. It references no CloudSync project
and no package.

---

### Allowed

```text
Daemon
    ↓
SyncEngine
    ↓
Ports
    ↓
Domain
```

```text
Persistence
    ↓
Ports
    ↓
Domain
```

```text
OneDrive
    ↓
Ports
    ↓
Domain
```

```text
UI
    ↓
IPC
    ↓
Domain
```

```text
Daemon
    ↓
Classification
    ↓
Ports
    ↓
Domain
```

---

### Forbidden

```text
Domain
    ↓
Anything
```

```text
Ports
    ↓
SyncEngine, Persistence, OneDrive, IPC, Daemon, UI, Observability
```

```text
Domain, IPC, UI, Observability
    ↓
Ports
```

```text
UI
    ↓
Persistence
```

```text
UI
    ↓
OneDrive
```

```text
OneDrive
    ↓
Persistence
```

```text
Persistence
    ↓
SyncEngine
```

```text
Classification
    ↓
Persistence, SyncEngine, OneDrive, IPC, UI, Observability, Daemon
```

```text
Domain, Ports, Persistence, SyncEngine, OneDrive, IPC, UI, Observability
    ↓
Classification
```

```text
Provider
    ↓
Sync Decisions
```

```text
UI
    ↓
DbContext
```

---

## Architectural Fitness Functions

The following statements should always remain true:

### Sync Engine Isolation

```text
The Sync Engine can be tested
with a fake provider,
a fake persistence layer
and no UI.
```

### UI Isolation

```text
The UI can be removed
without affecting synchronisation.
```

### Provider Isolation

```text
A new provider can be added
without modifying the
core reconciliation logic.
```

### Persistence Isolation

```text
Persistence changes do not
require Sync Engine changes.
```

---

## Aspire Guidance

Aspire is treated as:

```text
Developer Tooling
Application Composition
Observability Platform
```

Not:

```text
Business Architecture
Domain Logic
```

AStarDev.CloudSync must still run correctly if AppHost and Aspire are removed and the daemon is launched directly.

---

## Architecture Review Checklist

Before merging significant changes, verify:

```text
□ Domain has no dependencies

□ Ports contains contracts only, no implementations

□ SyncEngine depends only on Domain abstractions

□ UI communicates only through IPC

□ UI does not access persistence

□ Providers do not update persistence directly

□ Providers do not contain sync logic

□ Persistence contains no reconciliation logic

□ State mutation occurs only through SyncEngine

□ Error handling follows Document 10

□ New modules have clear ownership

□ Dependency rules remain valid

□ SyncEngine remains testable using fake providers
```

---

## Architectural Summary

```text
Domain
    ↓
SyncEngine
    ↓
Providers / Persistence

Daemon
    ↓
Composes Everything

UI
    ↓
IPC
    ↓
Daemon
```

The primary architectural goal is preventing erosion over time.

The easiest path for contributors should also be the correct path.

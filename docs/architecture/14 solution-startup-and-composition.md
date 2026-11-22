# 14 Solution Startup and Composition

## Architectural Principles

### Composition Happens At The Edge

Business logic must not construct its own dependencies.

Composition occurs only at application boundaries.

Examples:

```text
Daemon
AppHost
Tests
```

The Domain and Sync Engine must remain composition-agnostic.

---

### Dependency Injection Is A Runtime Concern

The Domain should not know:

```text
DI Containers
Service Providers
Hosting Infrastructure
```

The Domain should only know abstractions.

---

### Startup Must Be Deterministic

AStarDev.CloudSync should start the same way every time.

There should be a clearly defined bootstrap sequence.

---

## Runtime Topology

```text
AStarDev.CloudSync.AppHost
        ↓

 ┌───────────────┐
 │   Daemon      │
 └───────┬───────┘
         │
         ├── SyncEngine
         ├── Persistence
         ├── OneDrive
         ├── IPC Server
         └── Observability

 ┌───────────────┐
 │      UI       │
 └───────┬───────┘
         │
         └── IPC Client
```

---

## Startup Sequence

### Stage 1 - Process Start

```text
Host Starts
     ↓
Configuration Loaded
     ↓
Logging Initialised
     ↓
Single-Instance Lock Claimed
```

The lock lives beside the IPC socket and is held until the host stops. A second daemon for the same socket fails here, before it touches the database or the running daemon's socket.

Failure here is fatal.

---

### Stage 2 - Infrastructure Initialisation

```text
Telemetry
Persistence
IPC
Providers
```

are registered.

No synchronisation begins.

---

### Stage 3 - State Recovery

```text
Load Provider State
Load Sync Jobs
Load Pending Operations
Load Open Conflicts
```

---

### Stage 4 - Queue Recovery

```text
Find Running Operations
        ↓
Mark Failed
        ↓
Requeue Eligible Work
```

---

### Stage 5 - Background Services

Start:

```text
Operation Scheduler
Operation Workers
Provider Polling
Delta Processing
```

---

### Stage 6 - Accept Connections

```text
Start IPC Server
        ↓
Accept UI Connections
```

Daemon is now ready.

---

## Composition Root

The primary composition root is:

```text
AStarDev.CloudSync.Daemon
```

Responsibilities:

- Service registration
- Lifetime management
- Hosted service registration
- Infrastructure wiring

---

## Aspire Responsibilities

AStarDev.CloudSync.AppHost is responsible for:

```text
Process Orchestration
Observability Wiring
Development Diagnostics
Local Startup Experience
```

Must Not Own:

```text
Business Rules
Sync Rules
Provider Behaviour
```

---

## Hosted Services

Typical hosted services:

```text
QueueSchedulerService
DeltaProcessingService
ProviderHealthService
RecoveryService
```

Each service should have a single responsibility.

---

## Lifetime Guidelines

### Singleton

Examples:

```text
Operation Queue
Event Journal
Telemetry Services
```

### Scoped

Examples:

```text
Operation Execution Context
Database Unit Of Work
```

### Transient

Examples:

```text
Operation Handlers
Strategies
Mappers
```

---

## Configuration

Configuration must be strongly typed.

Example:

```csharp
OneDriveOptions
DatabaseOptions
TelemetryOptions
IpcOptions
```

Avoid string-based configuration lookups throughout the codebase.

---

## Health Checks

The daemon should expose health information for:

```text
Database
Provider Connectivity
Queue Health
IPC Availability
```

Health reporting supports both diagnostics and observability.

---

## Test Composition

Tests should be able to replace:

```text
Persistence
Provider
Telemetry
Time
```

with test doubles.

This enables deterministic testing.

---

## Architectural Invariants

```text
Domain is never resolved from DI.
```

```text
Composition occurs at application boundaries only.
```

```text
The daemon owns runtime orchestration.
```

```text
Business logic remains independent of hosting.
```

---

## Architecture Review Checklist

```text
□ Composition occurs only in composition roots

□ Domain has no DI dependencies

□ Hosted services have a single responsibility

□ Startup sequence remains deterministic

□ Infrastructure can be replaced in tests

□ Aspire remains a tooling concern

□ Daemon remains the runtime owner

□ No business logic depends on hosting abstractions
```

---

## Architectural Summary

```text
Domain
    ↓
SyncEngine
    ↓
Infrastructure

Daemon
    ↓
Composes Runtime

AppHost
    ↓
Composes Processes
```

The objective is to ensure that startup, hosting and infrastructure concerns remain separate from business behaviour.

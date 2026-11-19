# 15 Architecture Testing and Governance

## Architectural Principles

```text
**MUST** use AStarDev.CloudSync.TestsArchitecture for the architectural tests. Do *NOT* mix in with Unit / Integration tests.
```

### Architecture Must Be Enforced

Architecture documentation alone is insufficient.

Important architectural rules must be executable and continuously verified.

Principle:

```text
If a rule is important enough to document,
it is important enough to test.
```

---

### Build-Time Enforcement Is Preferred

Architectural violations should be detected:

```text
During Development
    ↓
During CI
    ↓
Before Merge
```

rather than months later.

---

### Correctness Over Convenience

Temporary shortcuts have a tendency to become permanent.

Architectural boundaries should require an explicit decision to break.

---

## Governance Layers

### Layer 1 - Architecture Documents

Documents define:

- Principles
- Boundaries
- Ownership
- Dependency Rules

---

### Layer 2 - Architecture Tests

Architecture tests enforce:

- Dependency direction
- Module isolation
- Forbidden references
- Architectural invariants

---

### Layer 3 - Code Review

Reviews verify:

- Intent
- Clarity
- Ownership
- Exceptions

---

## Architecture Test Project

Recommended project:

```text
AStarDev.CloudSync.TestsArchitecture
```

Purpose:

```text
Executable Architecture
```

This project contains no business tests.

It exists solely to enforce architectural rules.

---

## Recommended Technology

Preferred:

```text
NetArchTest.Rules
```

Reason:

- Lightweight
- Easy to understand
- Easy to maintain
- Sufficient for AStarDev.CloudSync

ArchUnitNET remains a future option if more advanced modelling becomes necessary.

---

## Architectural Invariants

The following rules should fail the build if violated.

### Domain Depends On Nothing

```text
AStarDev.CloudSync.Domain
    ↓
No Infrastructure Dependencies
```

---

### UI Accesses State Through IPC Only

```text
UI
    ↓
Persistence
```

Forbidden.

---

### Providers Do Not Perform Reconciliation

```text
OneDrive
    ↓
Sync Decisions
```

Forbidden.

---

### SyncEngine Owns Synchronisation Logic

```text
Only SyncEngine
```

owns:

- Reconciliation
- Conflict Detection
- Operation Planning

---

## Example NetArchTest Rule

### Domain Must Not Reference Infrastructure

```csharp
using NetArchTest.Rules;
using Xunit;

public sealed class DomainTestsArchitecture
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var result = Types
            .InAssembly(typeof(ItemId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Graph",
                "Avalonia",
                "Microsoft.Extensions.Hosting",
                "OpenTelemetry")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
```

---

## Additional Recommended Rules

### UI Must Not Reference Persistence

```text
AStarDev.CloudSync.UI
    ↓
AStarDev.CloudSync.Persistence
```

Forbidden.

### OneDrive Must Not Reference Persistence

```text
AStarDev.CloudSync.OneDrive
    ↓
AStarDev.CloudSync.Persistence
```

Forbidden.

### Persistence Must Not Reference SyncEngine

```text
AStarDev.CloudSync.Persistence
    ↓
AStarDev.CloudSync.SyncEngine
```

Forbidden.

### SyncEngine Must Not Reference Microsoft Graph

```text
AStarDev.CloudSync.SyncEngine
    ↓
Microsoft.Graph
```

Forbidden.

---

## Architectural Fitness Functions

These measure architectural health over time.

Examples:

- Circular dependency checks
- Project dependency checks
- Module isolation checks
- Infrastructure leakage checks

---

## Architectural Exceptions

### Exceptions Must Be Explicit

Architectural exceptions require documentation.

Not sufficient:

```text
#pragma
SuppressWarning
```

Preferred:

```text
ArchitectureException.md
```

containing:

- Rule violated
- Justification
- Approval date
- Removal strategy

---

## Continuous Integration

Architecture tests should execute:

```text
Pull Requests
Build Validation
Main Branch
Release Builds
```

Failures should block merges.

---

## Architecture Review Checklist

```text
□ Architectural invariants are enforced by tests

□ Architecture tests run in CI

□ Exceptions are documented

□ Dependency rules remain valid

□ Domain remains infrastructure-free

□ UI communicates only through IPC

□ SyncEngine remains independently testable

□ Providers remain isolated from persistence
```

---

## Architectural Summary

Documents describe the architecture.

Architecture tests enforce the architecture.

The objective is to make architectural erosion difficult and immediately visible.

## CI/CD Principle

Architectural compliance is a prerequisite for compilation and testing.

```text
Architecture Tests
        ↓
Build
        ↓
Unit Tests
        ↓
Integration Tests
        ↓
Release
```

# All log messages use generated LoggerMessage: slices

PRD: [cloudsync-generated-log-messages.md](cloudsync-generated-log-messages.md).
Status: Draft plan. Date: 2026-11-22.

## Epic

Title: All log messages use generated LoggerMessage

Move every direct logger call in production code to a source-generated
LoggerMessage method with a unique event id in a per-module 1000-block, then
make a direct call a build error.

Goals:

- Every production log message is a [LoggerMessage] method
- Event ids are unique and inside their module's block
- A direct logger call fails the build

Out of scope:

- Changing what is logged or its level
- Test projects
- A shared logging library

Definition of done: every slice merged, every acceptance item below ticked with
evidence, and the independent verifier passes.

## Order

| Wave | Slice | Blocked by | Files |
|------|-------|------------|-------|
| 1 | B1 | none | 10 |
| 2 | B2 | B1 | 14 |
| 2 | B3 | B1 | 12 |
| 2 | B4 | B1 | 14 |
| 2 | B5 | B1 | 10 |
| 2 | B6 | B1 | 8 |
| 3 | B7 | B2, B3, B4, B5, B6 | 8 |

Critical path: B1 then B2 then B7.

## Slices

### B1: Event id blocks, uniqueness test and rule (enabling)

One 1000-block of event ids per module, a test that ids are unique and inside
their block, and the rule written down, so migration slices can run in parallel
without clashing.

PRD requirements covered: PRD 2 (ids), 6 (docs, rule text).

Scope:

- Blocks: SyncEngine 1000, IPC 2000, OneDrive 3000, Persistence 4000,
  Observability 5000, Daemon 6000, UI 7000, SingleInstance 8000
- Move `UiLog` from id 1 to 7000
- Architecture test over the generated log classes
- `CLAUDE.md` and the observability runbook state the rule: new log messages are
  `[LoggerMessage]` methods in the module's block

Out of scope:

- Migrating direct calls (B2 to B6)
- The CA1848 build failure (B7)

Acceptance criteria:

- AC1: Ids are unique across the solution
- AC2: Each id lies inside its module's block
- AC3: `UiLog` uses 7000

Blocked by: none. Wave 1. Parallel with: none. Estimated files: 10.

Conflict hotspots: `UiLog.cs`, `CLAUDE.md`, the observability runbook.

Verification:

- AC1: Ids are unique across the solution. Check: test
  `LogEventIdsArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- AC2: Each id lies inside its module's block. Check: test
  `LogEventIdsArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- AC3: `UiLog` uses 7000. Check: test `LogEventIdsArchitectureTestsShouldEnsure`
  in `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `LogEventIdsArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`: `be_unique_across_the_solution`,
  `fall_within_the_block_of_their_module`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 (ids), 6 (docs, rule
  text)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### B2: Migrate Daemon log calls

The 12 direct log calls in the Daemon module become `[LoggerMessage]` methods in
the 6000 block.

PRD requirements covered: PRD 1 (Daemon).

Scope:

- A `DaemonLog` internal static partial class; callers switched
- Level, wording and parameters unchanged unless moving requires it; no personal
  data in parameters

Out of scope:

- `Daemon/Startup` (B3)

Acceptance criteria:

- AC1: No direct `logger.Log*` call remains in the Daemon module outside Startup
- AC2: Existing logging tests pass

Blocked by: B1. Wave 2. Parallel with: B3, B4, B5, B6. Estimated files: 14.

Conflict hotspots: Daemon handlers; none shared with B3 to B6.

Verification:

- AC1: No direct `logger.Log*` call remains in the Daemon module outside
  Startup. Check: test `LoggingShould` in `AStarDev.CloudSync.Daemon.TestsUnit`;
  run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Existing logging tests pass. Check: test `LoggingShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `LoggingShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `log_through_the_generated_messages`, `keep_personal_data_out_of_logs`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1 (Daemon)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### B3: Migrate Daemon startup log calls

The 10 direct log calls under Daemon/Startup become `[LoggerMessage]` methods in
the 6000 block.

PRD requirements covered: PRD 1 (Daemon startup).

Scope:

- A `StartupLog` internal static partial class; `StartupSequence` and the stages
  switched

Acceptance criteria:

- AC1: No direct `logger.Log*` call remains under `Daemon/Startup`
- AC2: Startup sequence tests pass

Blocked by: B1. Wave 2. Parallel with: B2, B4, B5, B6. Estimated files: 12.

Conflict hotspots: Event ids: take the 6100 sub-range so B2 and B3 never
overlap.

Verification:

- AC1: No direct `logger.Log*` call remains under `Daemon/Startup`. Check: test
  `StartupSequenceShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Startup sequence tests pass. Check: test `StartupSequenceShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `StartupSequenceShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `log_each_stage_through_the_generated_messages`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1 (Daemon startup)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### B4: Migrate SyncEngine log calls

The 9 direct log calls in the SyncEngine module become `[LoggerMessage]` methods
in the 1000 block, beside `SyncLog` and `OperationLog`.

PRD requirements covered: PRD 1 (SyncEngine).

Scope:

- Extend or add a log class in the 1000 block; callers switched
- Existing ids 1000 to 1107 do not change because the telemetry contract and
  runbook cite them

Acceptance criteria:

- AC1: No direct call remains in SyncEngine
- AC2: `TelemetryContractShould` still finds event ids 1002 and 1005

Blocked by: B1. Wave 2. Parallel with: B2, B3, B5, B6. Estimated files: 14.

Conflict hotspots: Event ids in use 1000 to 1107; new ones start at 1200.

Verification:

- AC1: No direct call remains in SyncEngine. Check: test
  `OperationSchedulerShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.SyncEngine.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.SyncEngine.TestsUnit.dll`.
- AC2: `TelemetryContractShould` still finds event ids 1002 and 1005. Check:
  test `LoggingShould` in `AStarDev.CloudSync.TestsIntegration`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsIntegration/bin/Debug/net11.0/AStarDev.CloudSync.TestsIntegration.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `OperationSchedulerShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `log_through_the_generated_messages`
- `LoggingShould` in `AStarDev.CloudSync.TestsIntegration`:
  `carry_correlation_ids_on_operation_logs`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1 (SyncEngine)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### B5: Migrate SingleInstance log calls

The 7 direct log calls in the SingleInstance module become `[LoggerMessage]`
methods in the 8000 block.

PRD requirements covered: PRD 1 (SingleInstance).

Scope:

- A `SingleInstanceLog` internal static partial class; the guard and
  `PrimaryInstance` switched

Acceptance criteria:

- AC1: No direct call remains
- AC2: The no-personal-data logging test still passes

Blocked by: B1. Wave 2. Parallel with: B2, B3, B4, B6. Estimated files: 10.

Conflict hotspots: None.

Verification:

- AC1: No direct call remains. Check: test `SingleInstanceGuardShould` in
  `AStarDev.SingleInstance.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.SingleInstance.TestsUnit/bin/Debug/net11.0/AStarDev.SingleInstance.TestsUnit.dll`.
- AC2: The no-personal-data logging test still passes. Check: test
  `SingleInstanceGuardShould` in `AStarDev.SingleInstance.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.SingleInstance.TestsUnit/bin/Debug/net11.0/AStarDev.SingleInstance.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `SingleInstanceGuardShould` in `AStarDev.SingleInstance.TestsUnit`:
  `keep_a_data_folder_path_that_holds_personal_data_out_of_the_logs`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1 (SingleInstance)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### B6: Migrate Persistence and Observability log calls

The 2 direct calls in Persistence and 1 in Observability become
`[LoggerMessage]` methods in the 4000 and 5000 blocks.

PRD requirements covered: PRD 1 (Persistence, Observability).

Scope:

- A `PersistenceLog` and an `ObservabilityLog` internal static partial class

Acceptance criteria:

- AC1: No direct call remains in either module

Blocked by: B1. Wave 2. Parallel with: B2, B3, B4, B5. Estimated files: 8.

Conflict hotspots: None.

Verification:

- AC1: No direct call remains in either module. Check: test
  `DatabaseInitialiserShould` in `AStarDev.CloudSync.Persistence.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Persistence.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Persistence.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `DatabaseInitialiserShould` in `AStarDev.CloudSync.Persistence.TestsUnit`:
  `log_through_the_generated_messages`
- `TelemetryServiceCollectionExtensionsShould` in
  `AStarDev.CloudSync.Observability.TestsUnit`:
  `log_through_the_generated_messages`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1 (Persistence,
  Observability)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### B7: Enforce: CA1848 as an error and an architecture test

A direct logger call fails the build, so the rule cannot erode.

PRD requirements covered: PRD 3 and 6.

Scope:

- `dotnet_diagnostic.CA1848.severity = error` in `.editorconfig`
- An architecture test that no production assembly calls the `LoggerExtensions`
  log methods
- Docs and `CLAUDE.md` finalised

Acceptance criteria:

- AC1: The build has 0 warnings and 0 errors
- AC2: Adding `logger.LogInformation(...)` to production code fails the build

Blocked by: B2, B3, B4, B5, B6. Wave 3. Parallel with: none. Estimated files: 8.

Conflict hotspots: `.editorconfig`.

Verification:

- AC1: The build has 0 warnings and 0 errors. Check: test
  `LoggingArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- AC2: Adding `logger.LogInformation(...)` to production code fails the build.
  Check: test `LoggingArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `LoggingArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`:
  `have_no_direct_logger_calls_in_production_code`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 3 and 6
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

## Epic acceptance

- [ ] No direct logger call remains in production code and the build has 0
  warnings (slice B2 to B7)
- [ ] Adding a direct logger call fails the build (slice B7)
- [ ] The duplicate-event-id test fails when two methods share an id (slice B1)
- [ ] The no-personal-data logging tests pass (slice B2 to B6)

Independent verifier prompt:

Check each criterion in the epic acceptance checklist against the merged code.
Report pass or fail per criterion with evidence (test names, command output,
file and line). Change nothing.

## Assumptions and decisions

- Interview 2026-11-22: generated approach is [LoggerMessage]; SingleInstance is
  included; ids use a 1000-block per module (SyncEngine 1000, IPC 2000, OneDrive
  3000, Persistence 4000, Observability 5000, Daemon 6000, UI 7000,
  SingleInstance 8000).
- Default taken: the CA1848 error is switched on last (B7), because the build
  must stay at 0 warnings while modules migrate.
- File counts are estimates.

## Issue numbers

Epic: #168 (umbrella #207).

- B1: #189 Event id blocks, uniqueness test and rule (enabling)
- B2: #190 Migrate Daemon log calls
- B3: #191 Migrate Daemon startup log calls
- B4: #192 Migrate SyncEngine log calls
- B5: #193 Migrate SingleInstance log calls
- B6: #194 Migrate Persistence and Observability log calls
- B7: #195 Enforce: CA1848 as an error and an architecture test

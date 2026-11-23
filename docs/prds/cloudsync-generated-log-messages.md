# All log messages use generated LoggerMessage

Source: owner request, 2026-11-22 (no idea doc). Status: Draft. Date:
2026-11-22.

## Problem

Log messages are written two ways. Five modules use source-generated
`[LoggerMessage]` classes, while 41 direct `logger.Log*` calls remain. Direct
calls allocate on every call, scatter message text, and have no stable event id,
so logs are harder to search and to keep free of personal data.

## Goals

- Every log message in production code is a `[LoggerMessage]` method with a
  unique event id.
- The build fails if a direct `logger.Log*` call is added.
- Log output keeps the existing rule that personal data (paths, e-mail
  addresses) never appears in messages.

## Non-goals

- Changing what is logged or its level, beyond what moving requires.
- Test projects, which may log directly.
- A shared logging helper library; each module keeps its own log class.

## Current state

Existing log classes: `IpcLog`, `OneDriveLog`, `SyncLog`, `OperationLog` and
`UiLog`. Direct calls remain in Daemon (12), Daemon/Startup (10), SyncEngine
(9), SingleInstance (7), Persistence (2) and Observability (1). Event ids in
use: SyncEngine 1000-1107, IPC 2000-2002, OneDrive 3000-3004 and UI 1.
`SingleInstance` is a standalone module with its own `ILogger` dependency.

## Requirements

1. Must: migrate every direct `logger.Log*` call in production code to a
   `[LoggerMessage]` method in a per-module `internal static partial` log class,
   one module per slice: Daemon, Daemon startup, SyncEngine, Persistence,
   Observability and SingleInstance.
1. Must: event ids are unique across the solution; add a test that scans the
   generated log classes and fails on a duplicate.
1. Must: set CA1848 to error in `.editorconfig` so a direct call fails the
   build, and add an architecture test that production assemblies contain no
   call to the `LoggerExtensions` log methods.
1. Must: message parameters carry ids and counts, not paths or e-mail addresses;
   existing no-personal-data tests keep passing.
1. Should: an exception is passed to the generated method as the exception
   parameter, not formatted into the text.
1. Must: update the observability runbook and `CLAUDE.md` with the rule.

## Acceptance criteria

- `dotnet build` has 0 warnings and no direct log call remains in production
  code.
- Adding `logger.LogInformation(...)` to production code fails the build.
- The duplicate-event-id test fails when two methods share an id.
- The no-personal-data logging tests pass.

## Constraints and decisions

- 2026-11-22: use `[LoggerMessage]` source generation, the approach already in
  five modules; the old `LogMessage` helper is not used.
- 2026-11-22: includes the `SingleInstance` module.
- 2026-11-22: event ids use one 1000-block per module: SyncEngine 1000, IPC
  2000, OneDrive 3000, Persistence 4000, Observability 5000, Daemon 6000, UI
  7000 and SingleInstance 8000. The three existing blocks already fit; `UiLog`
  uses id 1 and moves to 7000.
- Log classes are `internal static partial`, as the existing ones are.

## Risks

- Moving messages can change text that tests or the runbook assert on; run the
  logging tests after each module.
- Event id ranges need a convention before many slices pick ids in parallel.

## Open questions

None.

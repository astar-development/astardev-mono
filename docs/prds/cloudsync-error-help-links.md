# Errors carry a help link and code

Source: owner request, 2026-11-22 (no idea doc). Status: Draft. Date:
2026-11-22.

## Problem

When something goes wrong a developer or support person has no stable code to
search for and no page to read about common causes. Errors are plain records or
exceptions with free text.

## Goals

- Every error the app can raise has a stable code and a link under
  `https://astar-development.co.uk/` where its common causes will be documented.
- The link is available wherever the error is shown: logs, the UI error panel,
  the IPC reply and `Exception.HelpLink`.

## Non-goals

- Writing the help pages. The target need not exist; the link can be fake.
- Localising the help content.
- Changing error behaviour or recovery.

## Current state

Eleven error unions: `ProviderError` and `StoreError` (Ports), `IpcError`,
`DatabaseError` (Persistence), `DeltaError`, `LocalObservationError` and
`ResolutionError` (SyncEngine), and `LocalisationError`, `NavigationError`,
`DesktopNotifierError` and `ThemeError` (UI). Daemon also has `StartupError`.
Thrown exceptions are few: `StartupFailedException` and about six
`InvalidOperationException` or `JsonException` sites in Daemon, UI, OneDrive,
SyncEngine and IPC. None carries a code or link. The UI error panel already has
a "diagnostics" action but no link.

## Requirements

1. Must: define the contract once: a stable error code and a link built as
   `https://astar-development.co.uk/errors/<code>`, in a small project that
   Ports, IPC, Persistence, SyncEngine, Daemon and UI can reference without
   breaking module-boundary rules.
1. Must: every case of every error union above exposes its code and link, one
   union per slice.
1. Must: every thrown exception sets `HelpLink` to its link.
1. Must: the IPC error reply carries the code so the UI can show the link (doc
   07 is updated).
1. Must: logs for an error include the code, and the UI error panel shows the
   link.
1. Must: an architecture test fails when an error case or exception type has no
   code or link, or two share a code.
1. Must: codes read `CS-<MODULE>-<NNNN>`, for example `CS-IPC-0003`.

## Acceptance criteria

- Every error case in the listed unions returns a non-empty code and a link
  starting `https://astar-development.co.uk/errors/`.
- Throwing site `Exception.HelpLink` equals that error's link.
- A UI error shows its link and code; a log line for it contains the code.
- The architecture test fails if a new error case omits its code.
- `dotnet build` has 0 warnings; all test assemblies pass.

## Constraints and decisions

- 2026-11-22: root is `https://astar-development.co.uk/`; detail pages are out
  of scope and the link may be fake.
- 2026-11-22: covers both thrown exceptions and `Result` error cases.
- 2026-11-22: shown in logs, the UI error panel and the IPC reply.
- 2026-11-22: the contract lives in its own small project,
  `AStarDev.CloudSync.Errors`; codes read `CS-<MODULE>-<NNNN>`.
- 2026-11-22: `Domain` has no errors today; none to cover until it gains one.
- Errors stay closed unions of records, matched exhaustively
  ([`CLAUDE.md`](../../CLAUDE.md)); the code is a property of each case.

## Risks

- Changing the IPC error shape is a wire-contract change; the UI and daemon ship
  together, so a version mismatch only matters while developing.
- Codes are public once published; renumbering later breaks bookmarks.

## Open questions

None.

# AStarDev.CloudSync MVP

Source: [architecture docs](../architecture/), whole-app MVP. Roadmap: [doc
16](<../architecture/16 implementation-phases-roadmap.md>). Status: Draft. Date:
2026-11-19.

## Problem

There is no usable OneDrive sync client for Linux desktop users who need
selective, trustworthy sync. The architecture (docs 00-16) is settled but only
the solution skeleton and architecture tests exist. Issues #9-#12 track the
remaining roadmap phases.

## Goals

- A user connects a OneDrive account, chooses folders, and those folders stay in
  sync with a local directory.
- The user can always see what the daemon is doing, whether it is making
  progress, and whether it needs them (doc 13).
- No user data is silently lost; ambiguity becomes a visible conflict.
- Sync survives UI close, daemon crash and restart.

## Non-goals

- Files On-Demand (doc 06).
- Providers other than OneDrive. Provider boundary stays clean (doc 09).
- Windows support, named-pipe transport.
- Several jobs per account. Each account has one job with many scopes; accounts are independent (see `cloudsync-multi-account.md`).
- Performance tuning beyond the 700k item scale target in doc 11.
- Release automation, upgrade strategy.

## Current state

- Done (#6, #8): all projects and `.TestsUnit` projects exist under
  `apps/desktop/AStarDev.CloudSync/`, plus
  `AStarDev.CloudSync.TestsArchitecture` enforcing the forbidden-dependency
  rules.
- Domain has only `ItemId`. SyncEngine, Persistence, OneDrive, IPC, Daemon,
  Observability are `Class1` stubs.
- UI is the Avalonia 12.1.6 (net11.0) template with `ViewLocator`.
- AppHost and ServiceDefaults exist under `apps/Aspire/`, not named per doc 11.
- Doc 11 lists an `AStarDev.CloudSync.TestsIntegration` project (fake provider,
  scale and recovery tests), renamed from `.Testing` to match the `Tests*`
  naming. It does not exist yet.

## Requirements

Phase 3, startup and composition (#9):

- P3-1 (must): Daemon is the composition root and runs the six-stage startup
  sequence of doc 14, accepting IPC only after recovery.
- P3-2 (must): IPC uses `IIpcServer`/`IIpcClient` over a Unix domain socket with
  length-prefixed JSON; commands, queries and events are separate.
- P3-3 (must): UI connects, queries initial state, subscribes to events, shows a
  disconnected state and retries if the daemon is unavailable.
- P3-4 (must): Daemon runs without AppHost; AppHost only orchestrates.
- P3-5 (must): Daemon ships a systemd user unit; closing the UI does not stop
  sync.
- P3-6 (must): Strongly typed options (`IpcOptions`, `DatabaseOptions` etc.) and
  health checks for database, provider, queue, IPC.

Phase 4, observability (#10):

- P4-1 (must): `ILogger`, `ActivitySource`, `Meter` and OpenTelemetry in Daemon,
  SyncEngine, Persistence, IPC and OneDrive; none in Domain.
- P4-2 (must): Logs carry correlation ids (`OperationId`, `ItemId`, `SyncJobId`,
  `AccountId`); semantic conventions before custom attributes.
- P4-3 (must): Queue, sync, provider and performance metrics of doc 12.
- P4-4 (must): Logs, metrics and traces visible in the Aspire dashboard.

Phase 5, first vertical slice (#11):

- P5-1 (must): `ConnectAccount` runs MSAL public-client login via system browser
  and loopback redirect.
- P5-2 (must): Refresh tokens are stored in Secret Service (libsecret); if it is
  unavailable, connect fails with a clear message, never a plaintext fallback.
- P5-3 (must): Daemon enumerates the drive root through `ICloudProvider` and the
  UI shows the item count.
- P5-4 (must): Provider exceptions are translated to the four exception types of
  doc 09; no Graph types cross the boundary.

Phase 6, expansion (#14-#20), in order:

- P6-1 (must): Remote enumeration of the folder tree for scope selection.
- P6-2 (must): Selective sync. Connecting syncs nothing; the user picks scopes
  (doc 06).
- P6-3 (must): Initial crawl is checkpointed, resumable, reports progress, and
  ends by storing a delta token. All three bootstrap modes: download, upload,
  merge.
- P6-4 (must): Delta processing is the normal mode; invalid token triggers
  scoped reindex, not a reset.
- P6-5 (must): Local changes come from a filesystem watcher plus a scan on
  startup and on watcher overflow.
- P6-6 (must): Durable operation queue with retry and backoff, dead letter,
  per-item locking, priorities, crash recovery (doc 05, doc 10).
- P6-7 (must): Reconciliation, rename detection and merge per docs 02-04.
  Executors never mutate sync state.
- P6-8 (must): Conflict Centre with keep local, keep remote, keep both, defer,
  and bulk actions. Every resolution is journaled.
- P6-9 (must): Tray icon with conflict badge and desktop notifications for the
  events listed in doc 13.
- P6-10 (should): `AStarDev.CloudSync.TestsIntegration` with `FakeCloudProvider`
  so the SyncEngine is testable without OneDrive.

Cross-cutting (every phase):

- X-1 (must): Architecture tests pass first in CI; new modules get rules.
- X-2 (must): Each slice ships with tests; one vertical slice per PR.
- X-3 (must): Progress is reported by the daemon, throttled; the UI never
  computes it.

## Acceptance criteria

- Phase 3: AppHost launch starts Daemon; UI shows connected, then shows
  disconnected after the daemon is killed and reconnects when it returns.
- Phase 4: one operation produces correlated log, metric and trace in the
  dashboard.
- Phase 5: a user connects an account and sees the remote item count.
- Phase 6: with a 700k item fake provider, kill the daemon mid-crawl and
  mid-operation; on restart it resumes without redoing completed work.
- A modify/modify change produces a conflict, never an overwrite; keep both
  yields the original plus a `(Conflict - yyyy-MM-dd HHmmss)` copy; a name clash
  gets a numeric suffix.
- A locked or access-denied file dead-letters and the rest of the queue
  continues.

## Constraints and decisions

Interview 2026-11-19:

- Scope is the whole MVP, as one PRD with a section per roadmap phase.
- Linux only; Unix domain socket transport.
- Length-prefixed JSON on the wire, no new RPC dependency.
- MSAL public client, system browser, loopback redirect; Azure app registration
  exists, personal Microsoft accounts only (`consumers` authority). Business
  accounts are out of scope.
- Systemd user service for the daemon outside AppHost.
- Watcher plus scan for local changes.
- Secret Service for tokens.
- Conflict copy name uses date and time without colons, e.g. `Report (Conflict -
  2026-11-19 081417).xlsx`; date-only would collide when one file conflicts
  twice in a day.
- MVP includes all three bootstrap modes, tray and notifications, and the full
  Conflict Centre with bulk actions.

From the docs, fixed: Domain depends on nothing; UI reaches state only via IPC;
only SyncEngine mutates state; providers do not reconcile; Aspire is not a
runtime dependency.

## Risks

- Scope: whole-app MVP is large. Phase 6 must stay vertical (doc 16) or
  subsystems get built in isolation.
- Tray and notifications on Linux vary by desktop environment (Avalonia tray
  support, StatusNotifier).
- Secret Service needs a D-Bus session; headless or minimal desktops fail
  connect.
- A false-positive rename corrupts state tracking (doc 04); prefer false
  negatives.

## Open questions

- Content fingerprint: use OneDrive `quickXorHash` or compute locally? Default:
  `quickXorHash` for remote, compute the same locally only when size and mtime
  differ.
- Where does `FakeCloudProvider` live? Default: in `TestsIntegration`; split
  into a shared test-support project only if `SyncEngine.TestsUnit` needs it.

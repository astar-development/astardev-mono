# CloudSync UI shows real data

Source: owner request, 2026-11-22 (no idea doc). Status: Draft. Date:
2026-11-22.

## Problem

Single instance now works, but about 95% of what the UI shows is placeholder or
sample text. Users cannot see whether sync is healthy, how far a crawl has got,
how much storage they have, or what happened. The data is missing in the daemon,
the IPC contract, the provider port and persistence as well as in the UI, so
this is a cross-module change.

## Goals

- Every number and status in the UI comes from the daemon, never from hard-coded
  or sample values.
- A user can answer the four dashboard questions of [doc
  17](<../architecture/17 dashboard-and-user-trust-overview.md>) from the UI
  alone.
- Metrics, Logs, Traces and Diagnostics work for a normal user with no Aspire
  dashboard or other developer tooling.
- A guard fails the build if placeholder or sample text returns.

## Non-goals

- Aspire or any external dashboard for end users. It does not exist for them.
- Providers other than OneDrive, and Windows.
- Changing the sync engine's behaviour; only reporting on it.
- Log-message and error-link changes; see [generated log
  messages](<cloudsync-generated-log-messages.md>) and [error help
  links](<cloudsync-error-help-links.md>).

## Current state

Real over IPC today: accounts, conflicts, queue (dead letters), activity, remote
folders and root summary, daemon status. Events exist for accounts, conflicts,
activity, daemon status and `CrawlProgressChanged`, which carries only
`ItemsIndexed` and `Completed`.

Placeholder or sample today:

- Dashboard: `PlaceholderDashboardSource` is always used because
  `IDashboardSource` is never registered; the numbers are the doc 17 wireframe.
- Status bar: provider health, queue activity, error state and last activity
  (`StatusBarViewModel`).
- Onboarding: estimated items, available storage, phase, item counts, throughput
  and remaining time (six "Not yet available" strings).
- Providers: `SampleProviderCatalogue` and `SampleProviderInsightsSource`; every
  card shows the same sample last sync, storage and activity.
- Provider error panel: correlation id text, because the daemon sends none.
- Settings: Appearance, Notifications and Advanced sections; retention is the
  fixed text "90 days (placeholder)".
- History: sample operations and a placeholder Audit tab.
- Advanced screens: Metrics, Logs, Traces and Diagnostics are stubs
  (`StubScreenViewModel`).
- `PlaceholderNotificationPresenter` is a working in-app list with a misleading
  name; the OS notification path already uses `notify-send`.

Missing below the UI: `ICloudProvider` has no drive or quota call; no dashboard
or status query; no throughput or estimate; no settings store;
`ProviderStateEntity` already holds `LastSyncUtc` and `LastScanUtc`;
`IEventJournal` exists but only holds events such as conflict detected and
resolved.

## Requirements

1. Must: add status data to the IPC contract (provider health, queue depth and
   activity, error state, last activity) as a query plus pushed events, and bind
   the status bar to it.
1. Must: add a dashboard snapshot query plus events, an `IDashboardSource` over
   IPC, and register it so the dashboard shows daemon values.
1. Must: the engine reports crawl and sync phase, items done and total,
   throughput and estimated remaining time; `CrawlProgressChanged` or a
   successor carries them; the onboarding sync step and the dashboard use them.
1. Must: extend `ICloudProvider` with a drive-facts call (storage used,
   remaining, total, and an item-count estimate where the provider has one);
   implement it for OneDrive from Graph, for the fake provider, and expose it
   over IPC for the onboarding review step.
1. Must: replace the sample provider catalogue and insights with real data:
   capabilities from the registered provider, and per account last sync, storage
   and recent activity from daemon data.
1. Must: provider errors carry the trace or correlation id from the daemon, and
   the error panel shows it.
1. Must: add a daemon settings store (new table and migration) with IPC get and
   set, and persist theme, notification preferences and audit retention through
   it. Retention defaults to 90 days and is applied by the daemon.
1. Must: persist an audit trail (extend the event journal or add a table),
   expose a query over IPC, and show it on the Audit screen. Conflict creation,
   modification and resolution are always recorded (doc 22).
1. Must: Audit is a rail destination of its own (eight destinations); doc 19 and
   doc 22 are updated and the History Audit tab is removed.
1. Must: History lists real completed and failed operations from the daemon.
1. Must: users can export activity and audit data from the UI, as doc 22
   requires; the daemon serves the data and the UI saves the file the user
   chooses.
1. Must: persist logs, spans and metric samples in SQLite in the daemon, prune
   by a retention setting, expose them over IPC, and show them on the Logs,
   Traces and Metrics screens. Diagnostics shows health-check results and daemon
   status.
1. Should: rename `PlaceholderNotificationPresenter` for what it is.
1. Must: delete `PlaceholderDashboardSource`, `SampleProviderCatalogue`,
   `SampleProviderInsightsSource`, `StubScreenViewModel` and every placeholder
   or sample localisation key and view.
1. Must: add a test that fails if any localisation value or view text contains
   "placeholder", "sample" or "not yet available".
1. Must: every new IPC type follows doc 07 and the public-surface tests; new
   code follows `CLAUDE.md` (`Result`, `Option`, cancellation, XML docs,
   `[LoggerMessage]`, no nulls inside the app).
1. Must: each slice is verifiable against the fake provider switch, so no
   OneDrive account is needed in tests.

## Acceptance criteria

- With the daemon running on the fake tree, the dashboard, status bar and
  onboarding progress change as the crawl proceeds and match the daemon's own
  counts.
- With the daemon stopped, the UI shows an explicit disconnected state, not
  stale or sample numbers.
- A real OneDrive account shows its own quota in the onboarding review.
- Two accounts' cards show different values.
- The Audit screen is reachable from the navigation rail and shows recorded
  entries; History has no Audit tab.
- Exporting activity or audit data writes a file whose rows match what the
  screen shows for the same filters.
- Changing theme, notification preferences or retention survives a UI and daemon
  restart.
- The Logs, Traces and Metrics screens show entries produced by the running
  daemon after a restart of the daemon.
- The placeholder-text test passes, and fails if a placeholder string is
  reintroduced.
- `dotnet build` has 0 warnings; all test assemblies pass.

## Constraints and decisions

- 2026-11-22: real data only; Aspire is not available to end users.
- 2026-11-22: storage figures come from the real provider (OneDrive quota),
  which extends the `ICloudProvider` port and its public surface.
- 2026-11-22: telemetry for the in-app screens is persisted in SQLite, in a
  separate file (`telemetry.db`) so its write load and pruning never touch
  `cloudsync.db`. Retention is 7 days; audit retention is 90 days.
- 2026-11-22: Audit becomes a rail destination (eight destinations). Doc 19 says
  its seven-screen list is authoritative and has no Audit, while doc 22 treats
  Audit as a peer of Activity and History; both docs are updated to match.
- 2026-11-22: export of activity and audit data (doc 22) is in this effort.
- 2026-11-22: "available storage" is the provider's remaining quota only; free
  local disk space is not shown.
- 2026-11-22: settings live in the daemon database and are read and written over
  IPC.
- 2026-11-22: dashboard and status bar query once on open, then apply pushed
  events.
- Architecture docs 07, 09, 11, 13, 17, 19 and 22 are the source of truth; any
  contradiction is raised before code changes. Docs 19 and 22 change as part of
  the Audit decision above.
- Domain stays free of `FunctionalParadigm`; new ports are `internal` unless on
  the approved surface.

## Risks

- Writing logs, spans and metrics to SQLite adds write load during a 700k item
  crawl. Needs batching and a size cap.
- A settings or audit migration touches the user's existing database; a
  migration must be tested on an existing file.
- Persisted logs must stay free of personal data (paths, e-mail addresses), as
  already required for log output.
- Adding an eighth rail destination touches `ScreenIds`, navigation and their
  tests.
- The IPC contract grows considerably; keep messages small and versioned.

## Open questions

None.

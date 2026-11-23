# CloudSync UI shows real data: slices

PRD: [cloudsync-real-ui-data.md](cloudsync-real-ui-data.md). Status: Draft plan.
Date: 2026-11-22.

## Epic

Title: CloudSync UI shows real data

Replace every placeholder and sample value in the CloudSync UI with real data
from the daemon, extending IPC, the provider port, persistence and the engine as
needed. Audit becomes a rail destination and export is included.

Goals:

- Every UI number and status comes from the daemon
- Metrics, Logs, Traces and Diagnostics work for an end user without Aspire
- A guard test fails if placeholder text returns

Out of scope:

- Aspire for end users
- Providers other than OneDrive
- Free local disk space

Definition of done: every slice merged, every acceptance item below ticked with
evidence, and the independent verifier passes.

## Order

| Wave | Slice | Blocked by | Files |
|------|-------|------------|-------|
| 1 | A1 | none | 18 |
| 1 | A2 | none | 16 |
| 1 | A3 | none | 14 |
| 1 | A4 | none | 20 |
| 1 | A5 | none | 18 |
| 1 | A9 | none | 12 |
| 1 | A11 | none | 20 |
| 2 | A6 | A4, A5 | 22 |
| 2 | A7 | A3, A5 | 18 |
| 2 | A8 | A3 | 20 |
| 2 | A10 | A1 | 22 |
| 2 | A12 | A1 | 20 |
| 2 | A15 | A2 | 22 |
| 2 | A16 | A2 | 22 |
| 2 | A17 | A2 | 20 |
| 2 | A18 | A4 | 14 |
| 3 | A13 | A12 | 22 |
| 4 | A14 | A11, A13 | 20 |
| 5 | A19 | A4, A6, A7, A8, A9, A10, A11, A13, A14, A15, A16, A17, A18 | 20 |

Critical path: A1 then A12 then A13 then A14 then A19.

## Slices

### A1: Settings store and IPC (enabling)

The daemon keeps user settings in cloudsync.db and serves them over IPC, each
with a documented default. Several later slices need this seam, so it is small
and first.

PRD requirements covered: PRD 7 (store and IPC); defaults for the retention
values in PRD 8 and 12.

Scope:

- `Settings` table and migration, with the model snapshot updated
- `ISettingsStore` port in Ports (public-surface list updated) and its
  Persistence implementation
- IPC `GetSettings` query and `SetSetting` command, daemon handlers
- Defaults: theme, notification preferences, audit retention 90 days, telemetry
  retention 7 days

Out of scope:

- Any UI screen (A10)
- Applying retention (A12, A15 to A17)

Acceptance criteria:

- AC1: A fresh database returns every default
- AC2: A saved value is still there after a new context is created
- AC3: An unknown setting or invalid value is a failure value, not an exception
- AC4: The migration applies to an existing database file

Blocked by: none. Wave 1. Parallel with: A2, A3, A4, A5, A9, A11. Estimated
files: 18.

Conflict hotspots: `DaemonServiceCollectionExtensions`, `IpcHostedService`
command mapping, the public-surface test lists,
`CloudSyncDbContextModelSnapshot.cs`. Expect trivial merge conflicts in the
surface test list; the later PR rebases.

Verification:

- AC1: A fresh database returns every default. Check: test `SettingsStoreShould`
  in `AStarDev.CloudSync.Persistence.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Persistence.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Persistence.TestsUnit.dll`.
- AC2: A saved value is still there after a new context is created. Check: test
  `SettingsHandlersShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC3: An unknown setting or invalid value is a failure value, not an exception.
  Check: test `MigrationsShould` in `AStarDev.CloudSync.TestsIntegration`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsIntegration/bin/Debug/net11.0/AStarDev.CloudSync.TestsIntegration.dll`.
- AC4: The migration applies to an existing database file. Check: test
  `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `SettingsStoreShould` in `AStarDev.CloudSync.Persistence.TestsUnit`:
  `return_the_default_for_a_setting_never_saved`,
  `persist_a_saved_value_across_a_new_context`,
  `return_a_failure_for_an_unknown_setting`
- `SettingsHandlersShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `answer_get_settings_with_the_defaults`, `store_a_value_set_over_ipc`,
  `honour_cancellation`
- `MigrationsShould` in `AStarDev.CloudSync.TestsIntegration`:
  `apply_the_settings_migration_to_an_existing_database`
- `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`: `ports and ipc surface lists include
  the new types`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 7 (store and IPC);
  defaults for the retention values in PRD 8 and 12
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A2: Telemetry store in a separate telemetry.db (enabling)

A second SQLite file, telemetry.db, holds logs, spans and metric samples with
batching, query and pruning, so the in-app Logs, Traces and Metrics screens have
somewhere to read from without touching the sync database.

PRD requirements covered: PRD 12 (store); decision: separate file, 7 day
retention.

Scope:

- `TelemetryDbContext`, its own migration and `Telemetry:Database:Path` option
  (default beside cloudsync.db)
- `ITelemetryStore` port: append batches, query by time, level and text, prune
  older than a retention
- A pruning background service and a bounded batching writer; a failed write is
  dropped and counted, never blocks sync
- Lives in the Persistence module unless the implementer finds a boundary
  reason; record the choice in doc 11

Out of scope:

- The writers that feed it (A15 to A17)
- Any IPC or UI

Acceptance criteria:

- AC1: Entries appended in a batch can be read back
- AC2: Entries older than the retention are pruned
- AC3: An unavailable telemetry database returns a failure value and the sync
  database is untouched
- AC4: Telemetry and sync databases are separate files

Blocked by: none. Wave 1. Parallel with: A1, A3, A4, A5, A9, A11. Estimated
files: 16.

Conflict hotspots: Persistence DI registration, the public-surface test list,
doc 11.

Verification:

- AC1: Entries appended in a batch can be read back. Check: test
  `TelemetryStoreShould` in `AStarDev.CloudSync.Persistence.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Persistence.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Persistence.TestsUnit.dll`.
- AC2: Entries older than the retention are pruned. Check: test
  `TelemetryDatabaseOptionsShould` in
  `AStarDev.CloudSync.Persistence.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Persistence.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Persistence.TestsUnit.dll`.
- AC3: An unavailable telemetry database returns a failure value and the sync
  database is untouched. Check: test `TelemetryDatabaseShould` in
  `AStarDev.CloudSync.TestsIntegration`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsIntegration/bin/Debug/net11.0/AStarDev.CloudSync.TestsIntegration.dll`.
- AC4: Telemetry and sync databases are separate files. Check: test
  `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `TelemetryStoreShould` in `AStarDev.CloudSync.Persistence.TestsUnit`:
  `append_and_read_back_log_entries`, `prune_entries_older_than_the_retention`,
  `return_a_failure_when_the_database_is_unavailable`
- `TelemetryDatabaseOptionsShould` in
  `AStarDev.CloudSync.Persistence.TestsUnit`:
  `default_to_a_file_beside_the_sync_database`
- `TelemetryDatabaseShould` in `AStarDev.CloudSync.TestsIntegration`:
  `keep_the_sync_database_unchanged_while_telemetry_is_written`
- `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`: `persistence surface list includes the
  new types`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 12 (store); decision:
  separate file, 7 day retention
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A3: Drive facts on the provider port (enabling)

ICloudProvider can report storage used, remaining and total, plus an item-count
estimate where the provider has one, from real OneDrive data. IPC and UI come
later; this is the seam several slices need.

PRD requirements covered: PRD 4 (port, OneDrive and fake implementations).

Scope:

- `ICloudProvider.GetDriveInfoAsync` returning `Result<ProviderDriveInfo,
  ProviderError>`; the item-count estimate is an `Option<long>`
- OneDrive implementation from the Graph drive quota
- Fake provider implementation with configurable values
- Ports public-surface list updated

Out of scope:

- IPC `GetDriveInfo` and any UI (A7, A8)

Acceptance criteria:

- AC1: OneDrive maps the Graph quota to used, remaining and total
- AC2: A Graph failure maps to a `ProviderError`
- AC3: The fake reports the values it was given
- AC4: Cancellation is honoured

Blocked by: none. Wave 1. Parallel with: A1, A2, A4, A5, A9, A11. Estimated
files: 14.

Conflict hotspots: `ICloudProvider` implementers (OneDrive, fake, test stubs),
the Ports surface list.

Verification:

- AC1: OneDrive maps the Graph quota to used, remaining and total. Check: test
  `OneDriveDriveInfoShould` in `AStarDev.CloudSync.OneDrive.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.OneDrive.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.OneDrive.TestsUnit.dll`.
- AC2: A Graph failure maps to a `ProviderError`. Check: test
  `FakeCloudProviderShould` in `AStarDev.CloudSync.TestsIntegration`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsIntegration/bin/Debug/net11.0/AStarDev.CloudSync.TestsIntegration.dll`.
- AC3: The fake reports the values it was given. Check: test
  `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- AC4: Cancellation is honoured. Check: test
  `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `OneDriveDriveInfoShould` in `AStarDev.CloudSync.OneDrive.TestsUnit`:
  `map_the_graph_quota_to_drive_info`,
  `return_a_provider_error_when_graph_fails`, `honour_cancellation`
- `FakeCloudProviderShould` in `AStarDev.CloudSync.TestsIntegration`:
  `report_the_configured_drive_info`
- `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`: `ports surface list includes
  ProviderDriveInfo`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 4 (port, OneDrive and
  fake implementations)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A4: Status contract and real status bar

The status bar shows daemon values: provider health, queue activity, error state
and last activity, and an explicit disconnected state when the daemon link is
down.

PRD requirements covered: PRD 1; acceptance: status updates, disconnected state.

Scope:

- IPC `GetSyncStatus` query and `SyncStatusChanged` event
- Daemon status aggregator over the operation store, health checks and activity
  tracker
- `StatusBarViewModel` bound to an `IStatusSource` over IPC; localisation keys;
  remove its placeholder properties

Out of scope:

- Dashboard (A6)

Acceptance criteria:

- AC1: Queue depth and last activity match the operation store
- AC2: Provider health follows the health checks
- AC3: With the daemon stopped the bar shows disconnected, not the last values

Blocked by: none. Wave 1. Parallel with: A1, A2, A3, A5, A9, A11. Estimated
files: 20.

Conflict hotspots: IPC surface test list, `IpcHostedService` mapping,
`UiServiceCollectionExtensions`, localisation JSON.

Verification:

- AC1: Queue depth and last activity match the operation store. Check: test
  `SyncStatusQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Provider health follows the health checks. Check: test
  `SyncStatusMessagesShould` in `AStarDev.CloudSync.IPC.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.IPC.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.IPC.TestsUnit.dll`.
- AC3: With the daemon stopped the bar shows disconnected, not the last values.
  Check: test `StatusBarViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`;
  run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.
- Manual (needs a person): run the daemon on the fake tree
  (`docs/runbooks/cloudsync-fake-provider.md`), start a sync and watch the
  values change, then stop the daemon and confirm the disconnected state.

Tests to write first:

- `SyncStatusQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `report_queue_depth_from_the_operation_store`,
  `report_provider_health_from_the_health_checks`,
  `publish_a_status_change_when_queue_activity_changes`
- `SyncStatusMessagesShould` in `AStarDev.CloudSync.IPC.TestsUnit`:
  `round_trip_a_status_view`
- `StatusBarViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_daemon_values`, `show_disconnected_when_the_daemon_link_is_down`
- `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`: `ipc surface list includes the new
  types`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1; acceptance: status
  updates, disconnected state
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A5: Sync progress, throughput and estimate (enabling)

The engine reports crawl and sync phase, items done and total, throughput and
estimated time remaining, carried by the progress event, so the dashboard and
onboarding can show real figures.

PRD requirements covered: PRD 3 (engine and event); consumers are A6 and A7.

Scope:

- `ProgressEstimator` in SyncEngine: sliding-window items per second and
  remaining time as an `Option`
- `CrawlProgressChanged` (or a successor) carries phase, total, throughput and
  estimate; IPC surface updated
- The initial crawl uses the provider's `TotalItems`

Out of scope:

- Any UI (A6, A7)

Acceptance criteria:

- AC1: Throughput is computed over the window, not since start
- AC2: No estimate is reported when the total is unknown
- AC3: Progress events carry the new fields

Blocked by: none. Wave 1. Parallel with: A1, A2, A3, A4, A9, A11. Estimated
files: 18.

Conflict hotspots: `CrawlProgressChanged` consumers (`IpcCrawlProgressSink`,
UI).

Verification:

- AC1: Throughput is computed over the window, not since start. Check: test
  `ProgressEstimatorShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.SyncEngine.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.SyncEngine.TestsUnit.dll`.
- AC2: No estimate is reported when the total is unknown. Check: test
  `CrawlProgressMessagesShould` in `AStarDev.CloudSync.IPC.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.IPC.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.IPC.TestsUnit.dll`.
- AC3: Progress events carry the new fields. Check: test `InitialCrawlShould` in
  `AStarDev.CloudSync.TestsIntegration`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsIntegration/bin/Debug/net11.0/AStarDev.CloudSync.TestsIntegration.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ProgressEstimatorShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `compute_throughput_over_the_window`,
  `estimate_remaining_time_from_throughput_and_total`,
  `report_no_estimate_when_the_total_is_unknown`
- `CrawlProgressMessagesShould` in `AStarDev.CloudSync.IPC.TestsUnit`:
  `round_trip_the_extended_progress_event`
- `InitialCrawlShould` in `AStarDev.CloudSync.TestsIntegration`:
  `report_progress_with_a_total_and_an_estimate`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 3 (engine and event);
  consumers are A6 and A7
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A6: Real dashboard

The dashboard shows daemon values for health, reconciliation stage, counts,
queue, running operations and recent events, and stays current through pushed
events.

PRD requirements covered: PRD 2; acceptance: dashboard updates with the crawl.

Scope:

- IPC `GetDashboardSnapshot` query and `DashboardChanged` event; daemon
  aggregator
- `IDashboardSource` becomes async with a cancellation token, implemented over
  IPC and registered in `AddCloudSyncUi`
- `DashboardViewModel` uses it; sample labelling removed from the dashboard

Out of scope:

- Deleting `PlaceholderDashboardSource` (A19)

Acceptance criteria:

- AC1: The dashboard numbers match the daemon's own counts during a fake-tree
  crawl
- AC2: With the daemon stopped the dashboard shows a disconnected state

Blocked by: A4, A5. Wave 2. Parallel with: A7, A8, A10, A12, A15, A16, A17, A18.
Estimated files: 22.

Conflict hotspots: IPC surface list, `UiServiceCollectionExtensions`,
localisation JSON.

Verification:

- AC1: The dashboard numbers match the daemon's own counts during a fake-tree
  crawl. Check: test `DashboardSnapshotQueryShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: With the daemon stopped the dashboard shows a disconnected state. Check:
  test `DashboardViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.
- Manual (needs a person): run the daemon on the fake tree
  (`docs/runbooks/cloudsync-fake-provider.md`), start a sync and watch the
  values change, then stop the daemon and confirm the disconnected state.

Tests to write first:

- `DashboardSnapshotQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `report_counts_from_the_stores`, `report_the_running_operations`,
  `publish_a_change_when_the_queue_moves`
- `DashboardViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_snapshot_from_the_daemon`, `apply_pushed_changes`,
  `show_disconnected_when_the_daemon_link_is_down`
- `UiServiceCollectionExtensionsShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `register_the_dashboard_source`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2; acceptance:
  dashboard updates with the crawl
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A7: Onboarding review and sync progress

The onboarding review step shows the real estimated item count and the
provider's storage quota, and the sync step shows real phase, counts, throughput
and remaining time.

PRD requirements covered: PRD 3 and 4 (UI side); acceptance: OneDrive quota in
the review step.

Scope:

- IPC `GetDriveInfo` query answered through the provider port
- `OnboardingViewModel` review and sync properties bound to real values; an
  absent estimate shows as unknown, never as placeholder text
- Remove the six 'Not yet available' strings

Acceptance criteria:

- AC1: The review step shows the provider quota
- AC2: The sync step values change as progress events arrive
- AC3: An unknown item count reads as unknown

Blocked by: A3, A5. Wave 2. Parallel with: A6, A8, A10, A12, A15, A16, A17, A18.
Estimated files: 18.

Conflict hotspots: Onboarding localisation JSON, IPC surface list.

Verification:

- AC1: The review step shows the provider quota. Check: test
  `DriveInfoQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: The sync step values change as progress events arrive. Check: test
  `OnboardingViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC3: An unknown item count reads as unknown. Check: test
  `OnboardingViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.
- Manual (needs a person and a real OneDrive account): open onboarding, reach
  the review step, and confirm the shown quota matches the account in OneDrive.

Tests to write first:

- `DriveInfoQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `answer_with_the_provider_drive_info`,
  `return_the_provider_error_as_a_failure`
- `OnboardingViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_drive_info_in_the_review_step`, `show_progress_in_the_sync_step`,
  `show_unknown_when_the_estimate_is_absent`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 3 and 4 (UI side);
  acceptance: OneDrive quota in the review step
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A8: Real provider catalogue and per-account insights

The Providers screen lists what the daemon really supports, and each account
card shows its own last sync, storage and recent activity.

PRD requirements covered: PRD 5; acceptance: two accounts differ.

Scope:

- IPC `GetProviders` (capabilities of the registered provider) and per-account
  insights from `ProviderStateEntity` (last sync), the drive info and recent
  activity
- `IProviderCatalogue` and `IProviderInsightsSource` implemented over IPC and
  registered

Out of scope:

- Deleting the Sample* classes (A19)

Acceptance criteria:

- AC1: Cards for two accounts show different values
- AC2: Capabilities come from the registered provider

Blocked by: A3. Wave 2. Parallel with: A6, A7, A10, A12, A15, A16, A17, A18.
Estimated files: 20.

Conflict hotspots: Providers localisation JSON, IPC surface list.

Verification:

- AC1: Cards for two accounts show different values. Check: test
  `ProvidersQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Capabilities come from the registered provider. Check: test
  `ProvidersViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ProvidersQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `report_the_capabilities_of_the_registered_provider`,
  `report_last_sync_per_account`
- `ProvidersViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_each_accounts_own_insights`, `list_the_catalogue_from_the_daemon`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 5; acceptance: two
  accounts differ
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A9: Correlation id on provider errors

A failed provider request carries the daemon's trace or correlation id and the
error panel shows it.

PRD requirements covered: PRD 6.

Scope:

- IPC failure replies carry the trace id
- `ProviderErrorViewModel` shows it and drops the placeholder text

Out of scope:

- Error codes and help links (Epic C, C3 and C10 follow this slice)

Acceptance criteria:

- AC1: A failed connect shows the id of the daemon trace that handled it

Blocked by: none. Wave 1. Parallel with: A1, A2, A3, A4, A5, A11. Estimated
files: 12.

Conflict hotspots: The IPC failure shape also changes in C3; C3 is blocked by
this slice.

Verification:

- AC1: A failed connect shows the id of the daemon trace that handled it. Check:
  test `ConnectAccountHandlerShould` in `AStarDev.CloudSync.Daemon.TestsUnit`;
  run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ConnectAccountHandlerShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `include_the_trace_id_in_a_failure`
- `FailureReplyShould` in `AStarDev.CloudSync.IPC.TestsUnit`:
  `round_trip_the_trace_id`
- `ProviderErrorViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_correlation_id_from_the_daemon`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 6
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A10: Real Settings screens

Appearance, Notifications and retention settings are real: loaded from the
daemon, saved through it, applied, and kept across restarts.

PRD requirements covered: PRD 7 (UI); acceptance: settings survive restarts.

Scope:

- Appearance: theme choice persisted and applied at start
- Notifications: preferences applied by `NotificationService`
- Retention values shown and editable
- The Advanced section hosts the Metrics, Logs, Traces and Diagnostics screens;
  placeholder borders removed from `SettingsView`

Out of scope:

- The screens' own data (A15 to A18)

Acceptance criteria:

- AC1: A changed theme and notification preference survive a UI and daemon
  restart
- AC2: Changing retention changes the daemon's value

Blocked by: A1. Wave 2. Parallel with: A6, A7, A8, A12, A15, A16, A17, A18.
Estimated files: 22.

Conflict hotspots: Settings localisation JSON, `SettingsView.axaml`.

Verification:

- AC1: A changed theme and notification preference survive a UI and daemon
  restart. Check: test `SettingsViewModelShould` in
  `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC2: Changing retention changes the daemon's value. Check: test
  `NotificationServiceShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `SettingsViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `load_the_values_from_the_daemon`, `save_a_changed_theme`,
  `apply_notification_preferences`
- `NotificationServiceShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `honour_the_notification_preferences`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 7 (UI); acceptance:
  settings survive restarts
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A11: Real History with filters

History lists real completed and failed operations from the daemon, filterable
by provider, operation type, status, date range and path.

PRD requirements covered: PRD 10.

Scope:

- IPC `GetHistory` query
- `HistoryViewModel` over IPC; sample operations removed

Out of scope:

- The Audit tab (A12, A13)

Acceptance criteria:

- AC1: Newest first
- AC2: Each filter narrows the list
- AC3: An empty history shows an empty state

Blocked by: none. Wave 1. Parallel with: A1, A2, A3, A4, A5, A9. Estimated
files: 20.

Conflict hotspots: IPC surface list, History localisation JSON.

Verification:

- AC1: Newest first. Check: test `HistoryQueryShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Each filter narrows the list. Check: test `HistoryViewModelShould` in
  `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC3: An empty history shows an empty state. Check: test
  `HistoryViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `HistoryQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `list_completed_and_failed_operations_newest_first`,
  `filter_by_status_and_date_range`, `filter_by_path`
- `HistoryViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_daemon_operations`, `show_an_empty_state`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 10
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A12: Audit trail and query

Significant actions are recorded in a persistent audit trail, retained for 90
days, and queryable over IPC.

PRD requirements covered: PRD 8; acceptance: audit entries visible.

Scope:

- Extend the event journal: conflict created, modified, resolved; authentication
  changes; reconnections; configuration and scope changes; user decisions
- `IEventJournal` query by time range and type; pruning by the audit retention
  setting
- IPC `GetAudit` query

Out of scope:

- The Audit screen (A13)

Acceptance criteria:

- AC1: A resolved conflict is recorded
- AC2: A scope change is recorded
- AC3: Entries older than the retention are pruned

Blocked by: A1. Wave 2. Parallel with: A6, A7, A8, A10, A15, A16, A17, A18.
Estimated files: 20.

Conflict hotspots: `IEventJournal`, Ports surface list, IPC surface list.

Verification:

- AC1: A resolved conflict is recorded. Check: test `EventJournalStoreShould` in
  `AStarDev.CloudSync.Persistence.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Persistence.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Persistence.TestsUnit.dll`.
- AC2: A scope change is recorded. Check: test `ConflictResolverShould` in
  `AStarDev.CloudSync.SyncEngine.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.SyncEngine.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.SyncEngine.TestsUnit.dll`.
- AC3: Entries older than the retention are pruned. Check: test
  `AuditQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `EventJournalStoreShould` in `AStarDev.CloudSync.Persistence.TestsUnit`:
  `list_entries_in_a_time_range`, `prune_entries_older_than_the_retention`
- `ConflictResolverShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `record_a_resolution_in_the_journal`
- `AuditQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `list_recorded_entries`, `filter_by_type`
- `SyncScopeHandlersShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `record_a_scope_change_in_the_journal`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 8; acceptance: audit
  entries visible
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A13: Audit as a rail destination

Audit is its own navigation rail destination showing the audit trail; the
History Audit tab is gone and docs 19 and 22 say so.

PRD requirements covered: PRD 8 (screen) and 9; decision: eight destinations.

Scope:

- `ScreenId.Audit`, rail entry, `AuditViewModel` and view over `GetAudit`,
  localisation
- Remove the History Audit tab
- Update doc 19 (authoritative list and illustrative layout) and doc 22 (where
  Audit lives)

Out of scope:

- Export (A14)

Acceptance criteria:

- AC1: The rail has eight destinations
- AC2: The Audit screen shows entries recorded by the daemon
- AC3: History has no Audit tab

Blocked by: A12. Wave 3. Parallel with: none. Estimated files: 22.

Conflict hotspots: `ScreenIds`, rail view, navigation tests, localisation JSON.

Verification:

- AC1: The rail has eight destinations. Check: test `NavigationServiceShould` in
  `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC2: The Audit screen shows entries recorded by the daemon. Check: test
  `ScreenRegistrationShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC3: History has no Audit tab. Check: test `AuditViewModelShould` in
  `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.
- Manual (needs a person): the rail shows eight destinations and Audit lists
  entries after a conflict is resolved.

Tests to write first:

- `NavigationServiceShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `navigate_to_the_audit_screen`
- `ScreenRegistrationShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `register_an_audit_screen`
- `AuditViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_entries_from_the_daemon`, `show_an_empty_state`
- `HistoryViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `not_offer_an_audit_tab`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 8 (screen) and 9;
  decision: eight destinations
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A14: Export activity and audit

Users can export activity, history and audit data to a file they choose, as doc
22 requires.

PRD requirements covered: PRD 11.

Scope:

- Daemon export queries returning CSV with stable columns, honouring the screen
  filters
- An `IFilePicker` (beside `IFolderPicker`) and export commands on History and
  Audit

Out of scope:

- Other formats

Acceptance criteria:

- AC1: An export has the same rows as the screen for the same filters
- AC2: A save failure shows an error

Blocked by: A11, A13. Wave 4. Parallel with: none. Estimated files: 20.

Conflict hotspots: Localisation JSON, UI DI registration.

Verification:

- AC1: An export has the same rows as the screen for the same filters. Check:
  test `ExportQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: A save failure shows an error. Check: test `ExportCommandShould` in
  `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ExportQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `export_audit_as_csv_with_a_header_and_escaping`, `respect_the_filters`
- `ExportCommandShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `write_the_file_the_user_chose`, `show_an_error_when_the_save_fails`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 11
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A15: Logs: store, query and screen

The daemon writes its logs to the telemetry store and the Logs screen shows
them, filterable, after a restart.

PRD requirements covered: PRD 12 (logs); acceptance: Logs screen shows entries
after a restart.

Scope:

- A logger provider that batches into `ITelemetryStore`; messages carry ids and
  counts only, no personal data
- IPC `GetLogs` (level, time, text filters)
- `LogsViewModel` and view replace the stub

Out of scope:

- Traces and Metrics (A16, A17)

Acceptance criteria:

- AC1: Logs written before a daemon restart appear after it
- AC2: A failing store never blocks logging
- AC3: No stored message contains a path or e-mail address

Blocked by: A2. Wave 2. Parallel with: A6, A7, A8, A10, A12, A16, A17, A18.
Estimated files: 22.

Conflict hotspots: Daemon DI registration, IPC surface list, localisation JSON.

Verification:

- AC1: Logs written before a daemon restart appear after it. Check: test
  `TelemetryLoggerProviderShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: A failing store never blocks logging. Check: test `LogsQueryShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC3: No stored message contains a path or e-mail address. Check: test
  `LogsViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `TelemetryLoggerProviderShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `write_log_entries_to_the_telemetry_store`,
  `drop_entries_without_blocking_when_the_store_fails`,
  `keep_personal_data_out_of_stored_entries`
- `LogsQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `filter_by_level_and_time`
- `LogsViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_entries_from_the_daemon`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 12 (logs); acceptance:
  Logs screen shows entries after a restart
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A16: Traces: store, query and screen

The daemon stores its spans and the Traces screen lists traces and their spans.

PRD requirements covered: PRD 12 (traces).

Scope:

- An activity listener that batches spans into `ITelemetryStore`
- IPC `GetTraces` (by time, trace id)
- `TracesViewModel` and view replace the stub

Acceptance criteria:

- AC1: A UI request and the daemon span it caused appear in one trace
- AC2: Spans survive a daemon restart

Blocked by: A2. Wave 2. Parallel with: A6, A7, A8, A10, A12, A15, A17, A18.
Estimated files: 22.

Conflict hotspots: Daemon DI registration, IPC surface list.

Verification:

- AC1: A UI request and the daemon span it caused appear in one trace. Check:
  test `TelemetrySpanListenerShould` in `AStarDev.CloudSync.Daemon.TestsUnit`;
  run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Spans survive a daemon restart. Check: test `TracesQueryShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `TelemetrySpanListenerShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `write_spans_to_the_telemetry_store`,
  `drop_spans_without_blocking_when_the_store_fails`
- `TracesQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `return_the_spans_of_one_trace`
- `TracesViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_traces_from_the_daemon`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 12 (traces)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A17: Metrics: store, query and screen

The daemon samples its metrics into the telemetry store and the Metrics screen
shows current values and recent history.

PRD requirements covered: PRD 12 (metrics).

Scope:

- A meter listener sampling the CloudSync meters at an interval into
  `ITelemetryStore`
- IPC `GetMetrics`
- `MetricsViewModel` and view replace the stub

Acceptance criteria:

- AC1: Queue gauges and counters appear with recent history
- AC2: Samples survive a daemon restart

Blocked by: A2. Wave 2. Parallel with: A6, A7, A8, A10, A12, A15, A16, A18.
Estimated files: 20.

Conflict hotspots: Daemon DI registration, IPC surface list.

Verification:

- AC1: Queue gauges and counters appear with recent history. Check: test
  `TelemetryMetricSamplerShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: Samples survive a daemon restart. Check: test `MetricsQueryShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `TelemetryMetricSamplerShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `sample_the_cloudsync_meters_into_the_store`
- `MetricsQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `return_recent_samples_for_an_instrument`
- `MetricsViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_current_values_and_recent_history`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 12 (metrics)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A18: Diagnostics screen

Diagnostics shows the health-check results, daemon version, state and uptime,
with no personal data.

PRD requirements covered: PRD 12 (diagnostics).

Scope:

- IPC `GetDiagnostics` query
- `DiagnosticsViewModel` and view replace the stub

Acceptance criteria:

- AC1: Each registered health check appears with its status
- AC2: No file path or account name appears

Blocked by: A4. Wave 2. Parallel with: A6, A7, A8, A10, A12, A15, A16, A17.
Estimated files: 14.

Conflict hotspots: IPC surface list.

Verification:

- AC1: Each registered health check appears with its status. Check: test
  `DiagnosticsQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: No file path or account name appears. Check: test
  `DiagnosticsViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `DiagnosticsQueryShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `report_each_health_check`, `leave_paths_and_account_names_out`
- `DiagnosticsViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_diagnostics_from_the_daemon`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 12 (diagnostics)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### A19: Remove placeholders and add the guard test

Every placeholder and sample class, key and view is gone, and a test fails if
placeholder text returns.

PRD requirements covered: PRD 13, 14 and 15; acceptance: guard test.

Scope:

- Delete `PlaceholderDashboardSource`, `SampleProviderCatalogue`,
  `SampleProviderInsightsSource`, `StubScreenViewModel`, `StubScreenView` and
  every placeholder or sample localisation key and view
- Rename `PlaceholderNotificationPresenter` to say what it is (the in-app
  notification list)
- Add the guard test

Acceptance criteria:

- AC1: No localisation value or view text contains 'placeholder', 'sample' or
  'not yet available'
- AC2: The guard test fails when such text is added

Blocked by: A4, A6, A7, A8, A9, A10, A11, A13, A14, A15, A16, A17, A18. Wave 5.
Parallel with: none. Estimated files: 20.

Conflict hotspots: Localisation JSON files; this is the last slice, so it
rebases on everything.

Verification:

- AC1: No localisation value or view text contains 'placeholder', 'sample' or
  'not yet available'. Check: test `NoPlaceholderTextShould` in
  `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC2: The guard test fails when such text is added. Check: test
  `NoPlaceholderTextShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet
  exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `NoPlaceholderTextShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `not_appear_in_any_localisation_value`, `not_appear_in_any_view`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 13, 14 and 15;
  acceptance: guard test
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

## Epic acceptance

- [ ] Dashboard, status bar and onboarding progress change as a fake-tree crawl
  proceeds and match the daemon's counts (slice A4, A5, A6, A7)
- [ ] With the daemon stopped the UI shows an explicit disconnected state (slice
  A4, A6)
- [ ] A real OneDrive account shows its own quota in the onboarding review
  (slice A3, A7 (manual, needs a real account))
- [ ] Two accounts' cards show different values (slice A8)
- [ ] Settings survive a UI and daemon restart (slice A1, A10)
- [ ] The Audit screen is on the rail and shows recorded entries; History has no
  Audit tab (slice A12, A13)
- [ ] An export has the same rows as the screen for the same filters (slice A14)
- [ ] Logs, Traces and Metrics show entries produced by the daemon after a
  restart (slice A2, A15, A16, A17)
- [ ] The placeholder-text guard passes and fails when text is reintroduced
  (slice A19)
- [ ] Build has 0 warnings; every test assembly passes (slice every slice)

Independent verifier prompt:

Check each criterion in the epic acceptance checklist against the merged code.
Report pass or fail per criterion with evidence (test names, command output,
file and line). Change nothing.

## Assumptions and decisions

- Interview 2026-11-22: telemetry persisted in SQLite in a separate
  telemetry.db; settings in the daemon database over IPC; dashboard and status
  query once then apply events; Audit is a rail destination (eight destinations,
  docs 19 and 22 updated); export is in this effort; storage is provider quota
  only; audit retention 90 days and everything else 7 days.
- Defaults taken: the telemetry store lives in the Persistence module unless the
  implementer finds a boundary reason (A2); export format is CSV (A14); a
  missing item-count estimate is shown as unknown (A7).
- File counts are estimates.
- Cross-epic: from B1, new log messages are generated; from C1, new error cases
  carry a code. A slice merged before C1 is swept by C11.

## Issue numbers

Epic: #167 (umbrella #207).

- A1: #170 Settings store and IPC (enabling)
- A2: #171 Telemetry store in a separate telemetry.db (enabling)
- A3: #172 Drive facts on the provider port (enabling)
- A4: #173 Status contract and real status bar
- A5: #174 Sync progress, throughput and estimate (enabling)
- A6: #175 Real dashboard
- A7: #176 Onboarding review and sync progress
- A8: #177 Real provider catalogue and per-account insights
- A9: #178 Correlation id on provider errors
- A10: #179 Real Settings screens
- A11: #180 Real History with filters
- A12: #181 Audit trail and query
- A13: #182 Audit as a rail destination
- A14: #183 Export activity and audit
- A15: #184 Logs: store, query and screen
- A16: #185 Traces: store, query and screen
- A17: #186 Metrics: store, query and screen
- A18: #187 Diagnostics screen
- A19: #188 Remove placeholders and add the guard test

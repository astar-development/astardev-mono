# AStarDev.CloudSync MVP: slices

PRD: [cloudsync-mvp.md](<cloudsync-mvp.md>). Status: Draft plan. Date:
2026-11-19.

## Epic

Title: CloudSync MVP.

Deliver the CloudSync MVP in 22 small vertical slices: a Linux desktop OneDrive
client with a headless daemon and an Avalonia UI. Slices follow the roadmap
phases in [doc 16](<../architecture/16 implementation-phases-roadmap.md>).

Goals: connect a personal OneDrive account, choose folders, keep them in sync,
never lose data, and always show progress.

Out of scope: Files On-Demand, other providers, Windows, multiple accounts or
jobs, release automation (see the PRD non-goals).

Placeholder issues #9-#12 and #14-#20 are superseded by this epic and its
slices. They are left open for the owner to close.

## Common checks and definition of done

Commands, taken from `.github/workflows/ci.yml`:

- Build gate: `dotnet build --configuration Release`
- Architecture gate: `dotnet test
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/AStarDev.CloudSync.TestsArchitecture.csproj
  --configuration Release`
- Unit gate: `dotnet test --solution AStarDev.slnx --configuration Release
  --filter-not-namespace "*Integration*" --filter-not-namespace
  "*Architecture*"`
- Integration gate: `dotnet test --solution AStarDev.slnx --configuration
  Release --filter-namespace "*Integration*"`
- One class: add `--filter-class "*<ClassName>"` to a test command.

Test conventions follow the existing project: xunit with Shouldly, a class named
`<Subject>Should...` and snake_case method names that read as a sentence (see
`ArchitectureTestsShouldEnsure`). Unit tests live in the module's `.TestsUnit`
project, which exists for every module.

Tests first: write the listed tests, watch them fail, then implement.

Coding rules, apply to every slice:

- Any method that can fail returns a monad from `AStarDev.FunctionalParadigm`,
  normally `Result<TResult, TError>`; use `Option<T>` for "may be absent".
  Expected failures are never signalled with exceptions or `null`. Error values
  are domain types, so where a slice below says "exception", read it as the
  matching error value.
- Wrap code that throws (BCL, Graph, sockets, files) in `Try.Run` or
  `Try.RunAsync` and map the failure to an error value. No `try`/`catch` blocks.
  Exceptions remain only for programmer errors and framework contracts such as
  an `IHostedService` that must fail the host.
- Every async method takes a `CancellationToken` as its last parameter, passes
  it to every call that accepts one, and honours cancellation. Tests cover
  cancellation.
- `AStarDev.FunctionalParadigm` is written fresh. Never copy code from an
  existing library. A type or combinator is added only by the slice that first
  needs it, test first, one type or combinator per file so parallel slices do
  not conflict.

Definition of done, copied into every slice issue:

- [ ] Build, architecture and unit gates pass (integration gate where listed)
- [ ] Every listed test exists and passes; manual checks done by a person
- [ ] Build has no new warnings
- [ ] No method that can fail returns a bare value, null or throws for expected
  failures
- [ ] Every async method takes and honours a `CancellationToken`, with a test
- [ ] No unrelated files changed
- [ ] Covered PRD requirement ids named in the PR
- [ ] PR description reports evidence (command output, test counts), not only a
  claim that it works

## Order

| Wave | Slice | Blocked by | Parallel with | Files |
|------|-------|------------|---------------|-------|
| 1 | S0 | none | none | 10 |
| 2 | S1 | S0 | S2, S7 | 12 |
| 2 | S2 | S0 | S1, S7 | 16 |
| 2 | S7 | S0 | S1, S2 | 10 |
| 3 | S3 | S1, S2 | S4, S8, S11 | 20 |
| 3 | S4 | S1 | S3, S8, S11 | 6 |
| 3 | S8 | S7 | S3, S4, S11 | 12 |
| 3 | S11 | S7 | S3, S4, S8 | 10 |
| 4 | S5 | S4 | S9, S12 | 12 |
| 4 | S9 | S8 | S5, S12 | 14 |
| 4 | S12 | S7, S11 | S5, S9 | 22 |
| 5 | S6 | S3, S5 | S10 | 10 |
| 5 | S10 | S3, S9 | S6 | 18 |
| 6 | S13 | S10, S12 | S14 | 18 |
| 6 | S14 | S10 | S13 | 18 |
| 7 | S15 | S13, S14 | none | 22 |
| 8 | S16 | S15 | none | 24 |
| 9 | S17 | S16 | S18, S19 | 16 |
| 9 | S18 | S16 | S17, S19 | 30+ |
| 9 | S19 | S16 | S17, S18 | 30+ |
| 10 | S20 | S18, S19 | none | 30+ |
| 11 | S21 | S20 | none | 30+ |

Critical path: S0, S7, S8, S9, S10, S13, S15, S16, S18, S20, S21.

## Slices

### S0: FunctionalParadigm Result, Unit and Try

- Wave 1. Blocked by: none. Parallel with: none. Est. files: 10.
- Why first: enabling slice. `nuget-packages/AStarDev.FunctionalParadigm` is an
  empty stub, and S1, S2 and S7 all return `Result` from methods that can fail.
- Covers: the coding rules above.
- In: fresh code, test first, nothing copied from an existing library.
  `Result<TResult, TError>` as a C# union of `Ok<TResult>` and `Fail<TError>`
  cases (the SDK compiles `union` without preview flags); `Unit`; `Try.Run` and
  `Try.RunAsync(operation, cancellationToken)` returning `Result<T, Exception>`,
  which capture thrown exceptions but rethrow `OperationCanceledException`; only
  the combinators (`Match`, `Map`, `Bind`, `MapError`) that S1, S2 or S7
  actually call, one per file. New `AStarDev.FunctionalParadigm.TestsUnit`
  project added to `AStarDev.slnx`.
- Out: `Option`, `Validation` and everything else, until a slice needs it.
- Hotspots: `AStarDev.slnx`.

Verification:

- Test (`AStarDev.FunctionalParadigm.TestsUnit`):
  `ResultShould.hold_a_success_value`, `ResultShould.hold_a_failure_error`,
  `ResultShould.match_exhaustively_on_both_cases`,
  `TryShould.return_the_value_when_the_operation_succeeds`,
  `TryShould.capture_a_thrown_exception_as_a_failure`,
  `TryShould.rethrow_operation_cancelled`,
  `TryShould.not_start_when_already_cancelled`.
- Architecture: add rule
  `the_functional_paradigm_library_references_no_cloudsync_project` in a new
  test file.
- Gates: build, architecture, unit.

### S1: Daemon host and composition root

- Wave 2. Blocked by: S0. Parallel with: S2, S7. Est. files: 12.
- Why first: enabling slice. Daemon is a class library today
  (`Microsoft.NET.Sdk`, no host), so nothing else can run in it.
- Covers: P3-1 (stages, filled in by later slices), P3-4, P3-6 (options and
  health mechanism).
- In: Daemon becomes a runnable generic host; `Program.cs` as composition root;
  ordered `IStartupStage` pipeline with the six stages of doc 14, later slices
  add real work to stages; `IpcOptions` with socket path default under
  `$XDG_RUNTIME_DIR`; health check registration; runs without AppHost.
- Out: IPC server, database.
- Hotspots: `Daemon.csproj` and `Program.cs` (S3, S5, S12 edit them).

Verification:

- Test (`Daemon.TestsUnit`):
  `StartupSequenceShould.run_stages_in_the_documented_order`,
  `StartupSequenceShould.stop_and_report_when_an_early_stage_fails`,
  `StartupSequenceShould.stop_when_cancelled`,
  `IpcOptionsShould.default_the_socket_path_to_the_xdg_runtime_dir`.
- Architecture: add rule `the_ui_does_not_reference_the_daemon`.
- Manual (person): `dotnet run --project
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon` starts and logs all
  six stages with no AppHost.
- Gates: build, architecture, unit.

### S2: IPC contract and Unix socket transport

- Wave 2. Blocked by: S0. Parallel with: S1, S7. Est. files: 16.
- Covers: P3-2.
- In: `IIpcServer`, `IIpcClient`; length-prefixed JSON framing; Unix domain
  socket transport; command, query and event envelopes kept separate; first
  messages `GetDaemonStatus` query with `DaemonStatusView`, and a
  `DaemonStatusChanged` event with long-lived subscription.
- Out: wiring into Daemon and UI (S3), Windows named pipes.
- Hotspots: none; only the IPC project changes.

Verification:

- Test (`IPC.TestsUnit`): `FrameCodecShould.round_trip_a_message_of_any_size`,
  `FrameCodecShould.reject_a_frame_over_the_maximum_size`,
  `UnixSocketTransportShould.serve_a_query_over_a_real_socket`,
  `UnixSocketTransportShould.push_events_to_a_subscriber`,
  `UnixSocketTransportShould.tell_the_client_when_the_server_goes_away`,
  `UnixSocketTransportShould.honour_cancellation_of_a_pending_request`.
- Architecture: add rule `ipc_does_not_depend_on_the_ui_daemon_or_persistence`
  (doc 11).
- Gates: build, architecture, unit.

### S3: Daemon serves IPC; UI connects and shows status

- Wave 3. Blocked by: S1, S2. Parallel with: S4, S8, S11. Est. files: 20.
- Covers: P3-1 (stage 6, IPC only after recovery), P3-3, P3-6 (IPC health).
- In: Daemon hosted service starting the IPC server in stage 6, answering
  `GetDaemonStatus`; UI references IPC, connection service with retry and
  backoff, status banner (Connected, Reconnecting, Disconnected), initial query
  then event subscription.
- Out: AppHost changes (S4), any sync features.
- Hotspots: UI `App.axaml.cs` and main window; Daemon `Program.cs`.

Verification:

- Test (`Daemon.TestsUnit`):
  `IpcHostedServiceShould.start_only_after_the_recovery_stages`,
  `IpcHostedServiceShould.answer_get_daemon_status`.
- Test (`UI.TestsUnit`):
  `ConnectionViewModelShould.show_disconnected_when_the_daemon_is_unavailable`,
  `ConnectionViewModelShould.reconnect_when_the_daemon_returns`,
  `ConnectionViewModelShould.query_state_then_subscribe_on_connect`.
- Manual (person): start the daemon, start the UI, see Connected; kill the
  daemon, see Disconnected; restart it, see Connected again.
- Gates: build, architecture, unit.

### S4: AppHost launches Daemon and UI; systemd user unit

- Wave 3. Blocked by: S1. Parallel with: S3, S8, S11. Est. files: 6.
- Covers: P3-4, P3-5.
- In: AppHost starts the Daemon project and the UI (today it starts only the
  UI), UI waits for the daemon; shared socket path; a systemd user unit
  `deploy/systemd/astardev-cloudsync.service` with install notes.
- Out: packaging, auto-start of the UI.
- Hotspots: `apps/Aspire/AStarDev.AppHost/AppHost.cs` (S5 edits it).

Verification:

- Test (`AStarDev.AppHost.TestsIntegration`):
  `AppHostShould.start_the_daemon_and_ui_resources`.
- Gate: `systemd-analyze --user verify
  deploy/systemd/astardev-cloudsync.service` exits 0.
- Manual (person): `aspire run` shows daemon and UI running; with the unit
  installed, `systemctl --user start astardev-cloudsync`, open then close the
  UI, the daemon keeps running. UI connection is covered by S3.
- Gates: build, architecture, unit.

### S5: Observability module and daemon telemetry wiring

- Wave 4. Blocked by: S4. Parallel with: S9, S12. Est. files: 12.
- Covers: P4-1 (Daemon), P4-2, P4-4.
- In: Observability exposes `AddCloudSyncTelemetry` (OpenTelemetry logs,
  metrics, traces, OTLP exporter from `TelemetryOptions`); sources and meters
  registered by the `AStarDev.CloudSync.*` name so other modules use only
  `System.Diagnostics` and never reference Observability; semantic-convention
  resource attributes; correlation scope helper for `OperationId`, `ItemId`,
  `SyncJobId`, `AccountId`; AppHost passes the OTLP endpoint to the daemon.
- Out: module instrumentation (each later slice instruments its own code).
- Hotspots: `AppHost.cs`, Daemon `Program.cs`.

Verification:

- Test (`Observability.TestsUnit`):
  `TelemetryRegistrationShould.register_the_cloudsync_sources_and_meters`,
  `CorrelationScopeShould.include_the_correlation_ids_in_log_state`,
  `TelemetryOptionsShould.reject_a_malformed_otlp_endpoint`.
- Architecture: the existing rule that Domain has no OpenTelemetry stays green.
- Manual (person): `aspire run`, daemon logs appear in the dashboard.
- Gates: build, architecture, unit.

### S6: IPC and daemon instrumentation with an end-to-end trace

- Wave 5. Blocked by: S3, S5. Parallel with: S10. Est. files: 10.
- Covers: P4-1 (IPC), P4-3 (daemon and IPC metrics), phase 4 acceptance.
- In: span per command, query and event; meter for requests, connections and
  duration; trace context carried in the IPC envelope so UI and daemon spans
  form one trace; UI exports telemetry using the existing config keys.
- Out: provider, SyncEngine, Persistence instrumentation (their own slices).
- Hotspots: IPC envelope type (S10 adds messages).

Verification:

- Test (`IPC.TestsUnit`):
  `IpcTelemetryShould.start_a_span_per_query_named_after_the_message`,
  `IpcTelemetryShould.propagate_trace_context_across_the_envelope`,
  `IpcTelemetryShould.count_requests_and_record_duration`.
- Manual (person): trigger `GetDaemonStatus` from the UI; in the dashboard see
  one trace with UI and daemon spans, a correlated log line and the request
  metric.
- Gates: build, architecture, unit.

### S7: Provider contract and domain types

- Wave 2. Blocked by: S0. Parallel with: S1, S2. Est. files: 10.
- Why first: enabling slice. Provider, UI, engine and fake all need these types.
- Covers: P5-4 (types and error values).
- In: Domain gets `RemoteItemId`, `RelativePath`, `AccountId`,
  `ProviderAccount`, `ProviderItem`, `ItemType`, `SyncScope`,
  `ProviderSnapshotPage`; `ProviderError`, a closed set of error values
  (`RateLimited` with `RetryAfter`, `Transient`, `AuthenticationFailed`,
  `Permanent`) in place of the exceptions in doc 09; `ICloudProvider` with
  `ConnectAsync` and `GetSnapshotAsync` only, each returning `Task<Result<T,
  ProviderError>>` and taking a `CancellationToken`. Other members are added by
  the slice that needs them.
- Out: any implementation.
- Hotspots: Domain files (S12, S15 add types).

Verification:

- Test (`Domain.TestsUnit`): `RemoteItemIdShould.compare_by_value`,
  `ProviderErrorsShould.be_a_closed_set_of_four_cases`,
  `ProviderErrorsShould.carry_retry_after_on_rate_limited`.
- Architecture: add rules `the_syncengine_does_not_depend_on_microsoft_graph`
  and `the_onedrive_provider_does_not_depend_on_the_syncengine` (doc 15, doc
  09).
- Gates: build, architecture, unit.

### S8: OneDrive sign-in with Secret Service token cache

- Wave 3. Blocked by: S7. Parallel with: S3, S4, S11. Est. files: 12.
- Covers: P5-1, P5-2, P5-4 (auth errors).
- In: `OneDriveOptions` (client id from configuration, `consumers` authority);
  MSAL public client with system browser and loopback redirect; token cache
  persisted in Secret Service behind an `ITokenCacheStore` seam; keyring
  unavailable gives an `AuthenticationFailed` error value with a clear message
  and never a file fallback; `ConnectAsync` returns
  `Task<Result<ProviderAccount, ProviderError>>` and honours its
  `CancellationToken`.
- Out: enumeration (S9), IPC and UI (S10).
- Hotspots: `OneDrive.csproj` package references.

Verification:

- Test (`OneDrive.TestsUnit`):
  `OneDriveOptionsShould.require_a_client_id_and_use_the_consumers_authority`,
  `TokenCacheShould.fail_with_a_clear_message_when_secret_service_is_unavailable`,
  `TokenCacheShould.never_fall_back_to_a_file`,
  `AuthenticationErrorsShould.map_msal_failures_to_authentication_failure`.
- Manual (person): sign in with a personal account through the browser;
  `secret-tool search` finds the cache entry and no token file exists under the
  app data directory.
- Gates: build, architecture, unit.

### S9: OneDrive root enumeration

- Wave 4. Blocked by: S8. Parallel with: S5, S12. Est. files: 14.
- Covers: P5-3 (provider half), P5-4, P4-1 (OneDrive), P4-3 (provider metrics).
- In: `GetSnapshotAsync` for the drive root with paging; DriveItem to
  `ProviderItem` mapping so no Graph type crosses the boundary; HTTP errors
  translated (429 with Retry-After, 5xx, 401, 403); `ActivitySource` and `Meter`
  for requests, failures and rate limits.
- Out: UI and IPC (S10).
- Hotspots: none.

Verification:

- Test (`OneDrive.TestsUnit`, fake `HttpMessageHandler`):
  `DriveItemMappingShould.map_files_and_folders_to_provider_items`,
  `RootEnumerationShould.follow_next_links_until_the_last_page`,
  `GraphErrorsShould.map_429_to_a_rate_limited_error_with_retry_after`,
  `GraphErrorsShould.map_503_to_a_transient_error`,
  `GraphErrorsShould.map_401_to_an_authentication_failed_error`,
  `RootEnumerationShould.honour_cancellation`,
  `OneDriveTelemetryShould.record_a_span_and_a_request_count_per_page`.
- Gates: build, architecture, unit.

### S10: Connect an account and show the remote item count

- Wave 5. Blocked by: S3, S9. Parallel with: S6. Est. files: 18.
- Covers: P5-1, P5-3, phase 5 acceptance.
- In: `ConnectAccount` command, `GetRemoteRootSummary` query returning the item
  count, `AccountConnected` event; Daemon handler calls `ICloudProvider`; UI
  Connect button, busy state, item count, clear errors (sign-in failed, keyring
  missing). No database in this slice.
- Out: persistence of the account (S13).
- Hotspots: IPC message registry, UI main window, Daemon DI.

Verification:

- Test (`Daemon.TestsUnit`, stub provider):
  `ConnectAccountHandlerShould.publish_account_connected_after_connecting`,
  `ConnectAccountHandlerShould.reject_when_authentication_fails`,
  `ConnectAccountHandlerShould.honour_cancellation`,
  `RootSummaryQueryShould.return_the_item_count_from_the_provider`.
- Test (`UI.TestsUnit`):
  `ConnectViewModelShould.show_the_item_count_after_connecting`,
  `ConnectViewModelShould.explain_when_the_keyring_is_unavailable`.
- Manual (person): sign in with a real personal account; the UI count equals the
  number of items at the drive root.
- Gates: build, architecture, unit.

### S11: TestsIntegration project and FakeCloudProvider

- Wave 3. Blocked by: S7. Parallel with: S3, S4, S8. Est. files: 10.
- Why: enabling slice. Doc 11's `.Testing` project, named to match the `Tests*`
  convention; CI already runs namespaces matching `*Integration*`.
- Covers: P6-10.
- In: new `AStarDev.CloudSync.TestsIntegration` project added to
  `AStarDev.slnx`; `FakeCloudProvider` with an in-memory tree, generated large
  trees, paged snapshots and scripted failures (transient, rate limit, auth).
- Out: any scenario tests (later slices).
- Hotspots: `AStarDev.slnx`.

Verification:

- Test (`TestsIntegration`):
  `FakeCloudProviderShould.page_through_a_generated_tree_of_the_requested_size`,
  `FakeCloudProviderShould.fail_as_scripted_then_succeed`.
- Gate: integration gate runs these tests.
- Gates: build, architecture, unit, integration.

### S12: Persistence foundation

- Wave 4. Blocked by: S7, S11. Parallel with: S5, S9. Est. files: 22.
- Why: enabling slice. Persistence is a stub; the first real need is S13.
- Covers: P3-1 (stage 2 migrate and integrity check), P3-6 (database health),
  P4-1 (Persistence).
- In: EF Core and SQLite `DbContext`; entities and first migration for Accounts,
  SyncJobs, Items, ItemSnapshots and ProviderState per doc 01; `DatabaseOptions`
  (path under `$XDG_DATA_HOME`); Domain-side store interfaces for accounts only;
  startup applies migrations and runs an integrity check, failure enters
  recovery mode; database health check; persistence telemetry.
- Out: Operations, Conflicts, EventJournal tables (added by their slices).
- Hotspots: Daemon startup stages and DI; migrations folder (S13, S15, S16, S19
  each add one, sequenced by waves except S18 and S19).

Verification:

- Test (`TestsIntegration`, real SQLite):
  `MigrationsShould.create_the_schema_on_an_empty_database`,
  `IntegrityCheckShould.enter_recovery_mode_on_a_corrupt_database`.
- Test (`Persistence.TestsUnit`):
  `EntityMappingShould.round_trip_strongly_typed_ids`,
  `PersistenceTelemetryShould.record_a_span_per_save`.
- Architecture: the existing rule that Persistence does not reference the
  SyncEngine stays green.
- Gates: build, architecture, unit, integration.

### S13: Persist the account, reconnect on start, disconnect

- Wave 6. Blocked by: S10, S12. Parallel with: S14. Est. files: 18.
- Covers: P3-1 (stage 3 loads provider state), P3-6 (provider health), doc 10
  authentication expiry.
- In: connecting stores the account; stage 3 loads accounts and reconnects
  silently; token failure marks the account Reauthentication Required and pauses
  it; `GetAccounts` query, `DisconnectAccount` command and event (removes the
  account and its keyring entry); UI account card with Disconnect and Reconnect;
  provider health check.
- Out: sync jobs (S15).
- Hotspots: Daemon startup stages, IPC registry, UI main window.

Verification:

- Test (`Daemon.TestsUnit`):
  `StateRecoveryShould.reconnect_stored_accounts_silently`,
  `StateRecoveryShould.require_reauthentication_when_the_silent_token_fails`,
  `DisconnectAccountHandlerShould.remove_the_account_and_its_token`.
- Test (`TestsIntegration`):
  `AccountStoreShould.keep_an_account_across_a_restart`.
- Test (`UI.TestsUnit`):
  `AccountsViewModelShould.offer_reconnect_when_reauthentication_is_required`.
- Manual (person): restart the daemon; the account is still connected with no
  browser prompt.
- Gates: build, architecture, unit, integration.

### S14: Remote folder tree

- Wave 6. Blocked by: S10. Parallel with: S13. Est. files: 18.
- Covers: P6-1.
- In: `ICloudProvider.ListChildrenAsync(parent)` (and the `FakeCloudProvider`
  implementation); OneDrive listing with paging; `GetRemoteFolders(parent)`
  query; UI folder tree with lazy expansion, loading and error rows.
- Out: selecting folders (S15).
- Hotspots: `ICloudProvider` and `FakeCloudProvider`, UI main window.

Verification:

- Test (`OneDrive.TestsUnit`):
  `ChildrenListingShould.return_folders_for_a_parent`,
  `ChildrenListingShould.page_through_a_large_folder`.
- Test (`Daemon.TestsUnit`):
  `RemoteFoldersQueryShould.return_the_children_of_the_requested_parent`.
- Test (`UI.TestsUnit`):
  `FolderTreeViewModelShould.load_children_when_a_node_expands`,
  `FolderTreeViewModelShould.show_an_error_row_when_listing_fails`.
- Manual (person): browse a real OneDrive tree to two levels.
- Gates: build, architecture, unit.

### S15: Selective sync: jobs and scopes

- Wave 7. Blocked by: S13, S14. Parallel with: none. Est. files: 22.
- Covers: P6-2; stores the bootstrap mode chosen (acted on in S20).
- In: `SyncJob`, `SyncScope`, bootstrap mode (Download, Upload, Merge) in
  Domain; tables and migration; `CreateSyncJob` (validates the local root),
  `AddSyncScope`, `RemoveSyncScope`, `GetSyncJobs`, `GetSyncScopes`; UI ticks
  folders in the tree, picks the local folder and bootstrap mode; connecting
  alone creates no job; a second job is rejected (single job in the MVP).
- Out: crawling (S16).
- Hotspots: Domain, migrations, IPC registry, UI.

Verification:

- Test (`Daemon.TestsUnit`): `CreateSyncJobHandlerShould.reject_a_second_job`,
  `CreateSyncJobHandlerShould.reject_a_local_root_that_does_not_exist`,
  `ConnectAccountHandlerShould.create_no_sync_job`.
- Test (`TestsIntegration`):
  `SyncScopeStoreShould.keep_scopes_across_a_restart`.
- Test (`UI.TestsUnit`):
  `ScopeSelectionViewModelShould.start_with_nothing_selected`.
- Gates: build, architecture, unit, integration.

### S16: Initial crawl, checkpointed and resumable

- Wave 8. Blocked by: S15. Parallel with: none. Est. files: 24.
- Covers: P6-3 (crawl, checkpoint, delta token), P4-1 (SyncEngine), P4-3 (sync
  metrics), phase 6 acceptance (mid-crawl restart).
- In: SyncEngine crawler enumerates each scope through `GetSnapshotAsync`,
  persists Items and remote snapshots per page with a checkpoint, resumes from
  it after a restart, stores the delta token at the boundary; `StartSync` and
  `StopSync` commands; network failure pauses and retries; one scope failing
  does not stop the others; throttled `CrawlProgressChanged` events; spans per
  scope and page, indexed-items and crawl-duration metrics;
  `ProviderSnapshotPage` gains a resume token.
- Out: UI progress (S17), local scanning (S20).
- Hotspots: SyncEngine, `ICloudProvider`, migrations.

Verification:

- Test (`TestsIntegration`, `FakeCloudProvider`):
  `InitialCrawlShould.index_every_item_in_the_scope`,
  `InitialCrawlShould.resume_from_the_checkpoint_without_reindexing`,
  `InitialCrawlShould.store_a_delta_token_when_enumeration_completes`,
  `InitialCrawlShould.continue_other_scopes_when_one_fails`,
  `InitialCrawlShould.index_700000_items_with_bounded_memory`.
- Test (`SyncEngine.TestsUnit`):
  `CrawlProgressThrottleShould.emit_at_most_one_event_per_interval`.
- Manual (person): kill the daemon process mid-crawl on a large scope, restart,
  and see it resume rather than start over.
- Gates: build, architecture, unit, integration.

### S17: Crawl progress and activity in the UI

- Wave 9. Blocked by: S16. Parallel with: S18, S19. Est. files: 16.
- Covers: P6-3 (visible progress), X-3, doc 13 activity model.
- In: daemon `ActivityView` (name, state, progress, start, last update, action
  required) with ETA computed in the daemon; `GetActivities` query and event; UI
  shows Initial Indexing with count, percent and ETA, plus Working, Waiting
  (with retry seconds) and Healthy states.
- Out: notifications (S21).
- Hotspots: IPC registry, UI main window.

Verification:

- Test (`Daemon.TestsUnit`):
  `ActivityTrackerShould.report_progress_and_eta_for_a_crawl`,
  `ActivityTrackerShould.report_waiting_with_retry_seconds_when_rate_limited`.
- Test (`UI.TestsUnit`):
  `ActivityViewModelShould.render_daemon_progress_without_calculating_it`.
- Manual (person): watch a large crawl advance; close and reopen the UI, sync
  keeps going and progress is shown again.
- Gates: build, architecture, unit.

### S18: Delta processing (coarse, re-slice after S16)

- Wave 9. Blocked by: S16. Parallel with: S17, S19. Est. files: 30+.
- Covers: P6-4.
- Scope: delta mode as the normal mode; `GetDeltasAsync`; token lifecycle;
  invalid token triggers scoped reindex; `DeltaProcessingService`; updates
  remote snapshots only, no execution.
- Re-slice into vertical slices before starting; acceptance criteria and test
  names are written then. Provisional checks:
  `DeltaProcessingShould.apply_created_modified_deleted_and_renamed_items`,
  `DeltaProcessingShould.reindex_the_scope_when_the_token_is_invalid`.
- Hotspots: migrations (shared with S19), `ICloudProvider`.

### S19: Operation queue (coarse, re-slice after S16)

- Wave 9. Blocked by: S16. Parallel with: S17, S18. Est. files: 30+.
- Covers: P6-6, P3-1 (stage 4), P3-6 (queue health), phase 6 acceptance (mid
  operation restart, dead letter).
- Scope: durable operations table, scheduler, per-item locking, retry with
  backoff, dead letter, priorities, startup queue recovery, queue metrics,
  dead-letter list in the UI with retry and ignore.
- Re-slice before starting. Provisional checks:
  `OperationQueueShould.requeue_running_operations_after_a_restart`,
  `OperationQueueShould.dead_letter_one_file_and_keep_processing_the_rest`,
  `OperationQueueShould.never_run_two_operations_for_one_item_at_once`.
- Hotspots: migrations (shared with S18), Daemon startup stages.

### S20: Reconciliation and local changes (coarse, re-slice after S18, S19)

- Wave 10. Blocked by: S18, S19. Parallel with: none. Est. files: 30+.
- Covers: P6-5, P6-7, bootstrap modes (P6-3), phase 6 acceptance (conflict is
  detected, never an overwrite).
- Scope: filesystem watcher plus startup and overflow scans; quickXorHash;
  canonical state builder; reconciler, rename detection and merge per docs
  02-04; operation planner; executors; snapshot commit only by the SyncEngine;
  Download, Upload and Merge bootstrap behaviour; conflicts are created and
  persisted.
- Re-slice before starting. Provisional checks:
  `ReconcilerShould.produce_a_conflict_for_modify_modify`,
  `ReconcilerShould.merge_a_rename_with_a_content_change`,
  `RenameDetectionShould.prefer_a_missed_rename_to_a_false_rename`.
- Hotspots: SyncEngine, executors, OneDrive upload and download members.

### S21: Conflict handling, tray and notifications (coarse, re-slice after S20)

- Wave 11. Blocked by: S20. Parallel with: none. Est. files: 30+.
- Covers: P6-8, P6-9, phase 6 acceptance (keep both copy name).
- Scope: Conflicts and EventJournal tables; Conflict Centre UI with keep local,
  keep remote, keep both, defer and bulk actions; every resolution journaled;
  keep-both copy named `<name> (Conflict - yyyy-MM-dd HHmmss)<ext>` with a
  numeric suffix on a clash; tray icon with conflict badge; desktop
  notifications for the events in doc 13.
- Re-slice before starting. Provisional checks:
  `KeepBothShould.name_the_copy_with_date_and_time`,
  `KeepBothShould.add_a_numeric_suffix_when_the_name_exists`,
  `ConflictResolutionShould.journal_every_decision`.
- Hotspots: UI main window, IPC registry, migrations.

## Epic acceptance

Closing checklist, each item points at the slice and check that proves it:

- [ ] AppHost starts the daemon and UI; UI shows Disconnected when the daemon is
  killed and reconnects when it returns (S3 tests, S4 manual).
- [ ] The daemon runs as a systemd user service and survives closing the UI (S4
  manual).
- [ ] One operation yields a correlated log, metric and trace in the dashboard
  (S6 manual).
- [ ] A user connects a personal account and sees the remote item count (S10
  manual).
- [ ] Tokens are in Secret Service and never in a file (S8 manual).
- [ ] The account survives a daemon restart without a prompt (S13 manual).
- [ ] The user browses OneDrive, selects folders and nothing syncs before that
  (S14, S15).
- [ ] A 700000 item crawl survives a mid-crawl kill and resumes (S16).
- [ ] An operation killed mid-run is recovered and not repeated wrongly (S19).
- [ ] A locked or access-denied file dead-letters and the rest continues (S19).
- [ ] A modify and modify change produces a conflict, never an overwrite (S20);
  Keep Both gives the original plus a date-time named copy (S21).
- [ ] Progress is shown for long work and comes from the daemon (S17).
- [ ] Architecture tests pass first in CI throughout (every slice).
- [ ] Every method that can fail returns a `Result` or `Option`, and every async
  method takes and honours a `CancellationToken` (every slice).

Independent verifier prompt:

```text
Check each criterion in the epic acceptance checklist against the merged
code. Report pass or fail per criterion with evidence (test names and
results, file and line). Where a criterion needs a person, say so. Change
nothing.
```

## Assumptions and decisions

Interview 2026-11-19:

- New epic. Placeholders #9-#12 and #14-#20 are not reused or closed by this
  plan; the owner closes them.
- Phases 3-5 and Phase 6 steps 6.1-6.3 are fully sliced. Steps 6.4-6.7 are one
  coarse slice each, to be re-sliced once 6.3 lands.
- Persistence arrives at the start of Phase 6 (S12); Phase 5 has no database.
- Fingerprint: quickXorHash from OneDrive, computed locally only when size or
  modified time differs. Lands in S20.

- `AStarDev.FunctionalParadigm` is written fresh, only when required, no code
  copied from any existing library. `Result` is a C# union (SDK 11.0.100
  compiles it without preview flags). S0 is the first slice.
- Existing PRs #47, #48 and #49 are reworked in place by their agents after S0
  merges.

Defaults taken:

- File counts are estimates, not read from code that does not exist yet.
- `FakeCloudProvider` lives in `TestsIntegration` (S11); split it out only if a
  unit test project needs it.
- Modules use `System.Diagnostics` for sources and meters; only the daemon
  references Observability.
- Store interfaces live in Domain so SyncEngine and Persistence stay
  independent.
- Bootstrap mode is stored in S15 and acted on in S20.
- The Azure client id comes from configuration (`OneDrive:ClientId`) and is not
  committed.
- A local uncommitted change to `AStarDev.slnx` (adds
  `packages/AStarDev.FunctionalParadigm`) exists and is unrelated; this plan
  does not touch it.

## Issues

Epic: #23.

- S0: #50
- S1: #24
- S2: #25
- S3: #27
- S4: #28
- S5: #31
- S6: #34
- S7: #26
- S8: #29
- S9: #32
- S10: #35
- S11: #30
- S12: #33
- S13: #36
- S14: #37
- S15: #38
- S16: #39
- S17: #40
- S18: #41
- S19: #42
- S20: #43
- S21: #44

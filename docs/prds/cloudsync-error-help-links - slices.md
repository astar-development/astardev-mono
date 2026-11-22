# Errors carry a code and a help link: slices

PRD: [cloudsync-error-help-links.md](cloudsync-error-help-links.md). Status:
Draft plan. Date: 2026-11-22.

## Epic

Title: Errors carry a code and a help link

Give every error the app can raise a stable code and a link under
`https://astar-development.co.uk/errors/`, in logs, the IPC reply, the UI error
panel and Exception.HelpLink. The link target need not exist yet.

Goals:

- Every error union case and thrown exception has a code and link
- The link is visible in logs and the UI
- An architecture test stops new errors without a code

Out of scope:

- Writing the help pages
- Localised help content
- Changing error behaviour

Definition of done: every slice merged, every acceptance item below ticked with
evidence, and the independent verifier passes.

## Order

| Wave | Slice | Blocked by | Files |
|------|-------|------------|-------|
| 1 | C1 | none | 14 |
| 2 | C2 | C1 | 14 |
| 2 | C4 | C1 | 10 |
| 2 | C5 | C1 | 16 |
| 2 | C6 | C1 | 10 |
| 2 | C7 | C1 | 8 |
| 2 | C8 | C1 | 18 |
| 3 | C3 | C1, A9 | 20 |
| 4 | C9 | C2, C3, C4, C5, C6, C7, C8, B7 | 18 |
| 4 | C10 | C3, C8, A9 | 12 |
| 4 | C11 | C2, C3, C4, C5, C6, C7, C8 | 8 |

Critical path: C1 then C2 then C9.

## Slices

### C1: Errors project: code, link and helpers (enabling)

A small AStarDev.CloudSync.Errors project holds the error code, the link rule
and the helpers every module will use, so the module slices can run in parallel.

PRD requirements covered: PRD 1 (contract) and 7 (format).

Scope:

- `ErrorCode` (`CS-<MODULE>-<NNNN>`, validated), `IErrorInfo` (code and help
  link) and `ErrorLinks` (root `https://astar-development.co.uk/errors/`, link
  for a code, and a helper that sets `Exception.HelpLink`)
- Added to the solution; dependency and public-surface architecture tests;
  `AStarDev.CloudSync.Errors.TestsUnit`
- Docs 11 and 15 updated for the new module

Out of scope:

- Any module's error types (C2 to C8)

Acceptance criteria:

- AC1: A well-formed code is accepted and a malformed one is a failure value
- AC2: The link is under the documented root
- AC3: The helper sets `HelpLink`

Blocked by: none. Wave 1. Parallel with: none. Estimated files: 14.

Conflict hotspots: `AStarDev.slnx`, the architecture test lists.

Verification:

- AC1: A well-formed code is accepted and a malformed one is a failure value.
  Check: test `ErrorCodeShould` in `AStarDev.CloudSync.Errors.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Errors.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Errors.TestsUnit.dll`.
- AC2: The link is under the documented root. Check: test `ErrorLinksShould` in
  `AStarDev.CloudSync.Errors.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Errors.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Errors.TestsUnit.dll`.
- AC3: The helper sets `HelpLink`. Check: test
  `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ErrorCodeShould` in `AStarDev.CloudSync.Errors.TestsUnit`:
  `accept_a_well_formed_code`, `return_a_failure_for_a_malformed_code`
- `ErrorLinksShould` in `AStarDev.CloudSync.Errors.TestsUnit`:
  `build_the_link_under_the_documented_root`,
  `set_the_help_link_on_an_exception`
- `PublicSurfaceArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`: `errors surface list`
- `ArchitectureTestsShouldEnsure` in `AStarDev.CloudSync.TestsArchitecture`:
  `the_errors_project_depends_on_nothing_in_cloudsync`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 1 (contract) and 7
  (format)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C2: Ports errors carry code and link

Every case of ProviderError and StoreError exposes a stable code and its help
link.

PRD requirements covered: PRD 2 (Ports).

Scope:

- Cases implement `IErrorInfo` with codes in the CS-PORTS block
- OneDrive and fake provider unaffected apart from the types

Acceptance criteria:

- AC1: Every case returns a non-empty code and a link under the documented root
- AC2: Codes in the block are unique

Blocked by: C1. Wave 2. Parallel with: C4, C5, C6, C7, C8. Estimated files: 14.

Conflict hotspots: Ports surface list.

Verification:

- AC1: Every case returns a non-empty code and a link under the documented root.
  Check: test `ProviderErrorShould` in `AStarDev.CloudSync.Ports.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Ports.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Ports.TestsUnit.dll`.
- AC2: Codes in the block are unique. Check: test `StoreErrorShould` in
  `AStarDev.CloudSync.Ports.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Ports.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Ports.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ProviderErrorShould` in `AStarDev.CloudSync.Ports.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `StoreErrorShould` in `AStarDev.CloudSync.Ports.TestsUnit`:
  `expose_a_code_and_link_for_every_case`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 (Ports)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C3: IPC errors and the wire reply carry the code

Every IpcError case has a code and link, and the IPC failure reply carries the
code so the UI can show the link.

PRD requirements covered: PRD 2 (IPC), 3 (wire), 4.

Scope:

- IpcError cases implement `IErrorInfo` (CS-IPC block); `Envelopes`
  JsonException sets `HelpLink`
- The failure reply carries the code; doc 07 updated; the UI client maps it
- Follows A9, which also changes the failure shape

Out of scope:

- Showing it in the UI (C10)

Acceptance criteria:

- AC1: A failure reply round-trips its code
- AC2: Every case returns a code and link

Blocked by: C1, A9. Wave 3. Parallel with: none. Estimated files: 20.

Conflict hotspots: The IPC failure shape (also A9); IPC surface list; doc 07.

Verification:

- AC1: A failure reply round-trips its code. Check: test `IpcErrorShould` in
  `AStarDev.CloudSync.IPC.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.IPC.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.IPC.TestsUnit.dll`.
- AC2: Every case returns a code and link. Check: test `FailureReplyShould` in
  `AStarDev.CloudSync.IPC.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.IPC.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.IPC.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `IpcErrorShould` in `AStarDev.CloudSync.IPC.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `FailureReplyShould` in `AStarDev.CloudSync.IPC.TestsUnit`:
  `round_trip_the_error_code`
- `EnvelopesShould` in `AStarDev.CloudSync.IPC.TestsUnit`:
  `set_the_help_link_on_a_malformed_envelope`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 (IPC), 3 (wire), 4
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C4: Persistence errors carry code and link

DatabaseError cases and the Sqlite error mapping expose a code and link.

PRD requirements covered: PRD 2 (Persistence).

Scope:

- Cases implement `IErrorInfo` (CS-DB block); `SqliteErrors` mapping unchanged
  otherwise

Acceptance criteria:

- AC1: Every case returns a code and link

Blocked by: C1. Wave 2. Parallel with: C2, C5, C6, C7, C8. Estimated files: 10.

Conflict hotspots: None.

Verification:

- AC1: Every case returns a code and link. Check: test `DatabaseErrorShould` in
  `AStarDev.CloudSync.Persistence.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Persistence.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Persistence.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `DatabaseErrorShould` in `AStarDev.CloudSync.Persistence.TestsUnit`:
  `expose_a_code_and_link_for_every_case`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 (Persistence)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C5: SyncEngine errors and thrown exceptions carry code and link

DeltaError, LocalObservationError and ResolutionError cases, and the exception
thrown by the local file store, carry a code and link.

PRD requirements covered: PRD 2 and 3 (SyncEngine).

Scope:

- Cases implement `IErrorInfo` (CS-SYNC block)
- `PhysicalLocalFileStore` sets `HelpLink` on the exception it throws

Acceptance criteria:

- AC1: Every case returns a code and link
- AC2: The thrown exception has `HelpLink` set

Blocked by: C1. Wave 2. Parallel with: C2, C4, C6, C7, C8. Estimated files: 16.

Conflict hotspots: None.

Verification:

- AC1: Every case returns a code and link. Check: test `DeltaErrorShould` in
  `AStarDev.CloudSync.SyncEngine.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.SyncEngine.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.SyncEngine.TestsUnit.dll`.
- AC2: The thrown exception has `HelpLink` set. Check: test
  `LocalObservationErrorShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`;
  run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.SyncEngine.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.SyncEngine.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `DeltaErrorShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `LocalObservationErrorShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `ResolutionErrorShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `PhysicalLocalFileStoreShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `set_the_help_link_on_a_path_that_escapes_the_root`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 and 3 (SyncEngine)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C6: Daemon startup errors and exceptions carry code and link

StartupError and StartupFailedException (and the throw in IpcHostedService)
carry a code and link.

PRD requirements covered: PRD 2 and 3 (Daemon).

Scope:

- `StartupError` implements `IErrorInfo` (CS-DAEMON block);
  `StartupFailedException` sets `HelpLink`

Acceptance criteria:

- AC1: A failed startup stage exposes a code and link
- AC2: The host failure carries `HelpLink`

Blocked by: C1. Wave 2. Parallel with: C2, C4, C5, C7, C8. Estimated files: 10.

Conflict hotspots: None.

Verification:

- AC1: A failed startup stage exposes a code and link. Check: test
  `StartupHostedServiceShould` in `AStarDev.CloudSync.Daemon.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- AC2: The host failure carries `HelpLink`. Check: test `StartupErrorShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `StartupHostedServiceShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `set_the_help_link_on_the_startup_failure`
- `StartupErrorShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `expose_a_code_and_link`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 and 3 (Daemon)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C7: OneDrive thrown exceptions carry a link

The exceptions thrown by the OneDrive module's sign-in and token-cache code
carry a code and HelpLink.

PRD requirements covered: PRD 3 (OneDrive).

Scope:

- `MsalSilentSignIn` and `SecretToolTokenCacheStore` throws use the helper
  (CS-ONEDRIVE block)

Acceptance criteria:

- AC1: Each thrown exception has `HelpLink` set

Blocked by: C1. Wave 2. Parallel with: C2, C4, C5, C6, C8. Estimated files: 8.

Conflict hotspots: None.

Verification:

- AC1: Each thrown exception has `HelpLink` set. Check: test
  `SecretToolTokenCacheStoreShould` in `AStarDev.CloudSync.OneDrive.TestsUnit`;
  run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.OneDrive.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.OneDrive.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `SecretToolTokenCacheStoreShould` in `AStarDev.CloudSync.OneDrive.TestsUnit`:
  `set_the_help_link_when_the_tool_cannot_start`
- `MsalSilentSignInShould` in `AStarDev.CloudSync.OneDrive.TestsUnit`:
  `set_the_help_link_when_the_account_is_unknown`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 3 (OneDrive)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C8: UI errors and thrown exceptions carry code and link

LocalisationError, NavigationError, DesktopNotifierError and ThemeError cases,
and the UI's thrown exceptions, carry a code and link.

PRD requirements covered: PRD 2 and 3 (UI).

Scope:

- Cases implement `IErrorInfo` (CS-UI block)
- `UiServiceCollectionExtensions` and `NotifySendNotifier` throws set `HelpLink`

Out of scope:

- Showing the link (C10)

Acceptance criteria:

- AC1: Every case returns a code and link
- AC2: The thrown exceptions have `HelpLink` set

Blocked by: C1. Wave 2. Parallel with: C2, C4, C5, C6, C7. Estimated files: 18.

Conflict hotspots: UI DI registration.

Verification:

- AC1: Every case returns a code and link. Check: test `LocalisationErrorShould`
  in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC2: The thrown exceptions have `HelpLink` set. Check: test
  `NavigationErrorShould` in `AStarDev.CloudSync.UI.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `LocalisationErrorShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `NavigationErrorShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `DesktopNotifierErrorShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `expose_a_code_and_link_for_every_case`
- `ThemeErrorShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `expose_a_code_and_link_for_every_case`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 2 and 3 (UI)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C9: Error logs carry the error code

Every log message written for an error includes its code, so a developer can
search the log for the code and open the link.

PRD requirements covered: PRD 5 (logs).

Scope:

- The generated log methods for error paths take the code as a parameter
- Per-module logging tests assert the code

Acceptance criteria:

- AC1: A log line for an error contains its code

Blocked by: C2, C3, C4, C5, C6, C7, C8, B7. Wave 4. Parallel with: C10, C11.
Estimated files: 18.

Conflict hotspots: Every `*Log` class; follows Epic B so it edits generated
messages only.

Verification:

- AC1: A log line for an error contains its code. Check: test `LoggingShould` in
  `AStarDev.CloudSync.Daemon.TestsUnit`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.Daemon.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `LoggingShould` in `AStarDev.CloudSync.Daemon.TestsUnit`:
  `include_the_error_code_when_logging_an_error`
- `OperationSchedulerShould` in `AStarDev.CloudSync.SyncEngine.TestsUnit`:
  `include_the_error_code_when_logging_a_failure`
- `LoggingShould` in `AStarDev.CloudSync.TestsIntegration`:
  `include_the_error_code_on_operation_failures`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 5 (logs)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C10: UI error panel shows the code and link

The UI error panel shows the code and an action that opens the help link in the
browser.

PRD requirements covered: PRD 5 (UI).

Scope:

- `ProviderErrorViewModel` shows the code from the daemon reply and an open-link
  command through an `IUriLauncher`

Acceptance criteria:

- AC1: The panel shows the code and link of the daemon's failure
- AC2: Opening the link launches the browser through the launcher

Blocked by: C3, C8, A9. Wave 4. Parallel with: C9, C11. Estimated files: 12.

Conflict hotspots: `ProviderErrorViewModel`, also changed by A9 (which comes
first).

Verification:

- AC1: The panel shows the code and link of the daemon's failure. Check: test
  `ProviderErrorViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- AC2: Opening the link launches the browser through the launcher. Check: test
  `ProviderErrorViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`; run
  `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.UI.TestsUnit/bin/Debug/net11.0/AStarDev.CloudSync.UI.TestsUnit.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ProviderErrorViewModelShould` in `AStarDev.CloudSync.UI.TestsUnit`:
  `show_the_error_code_and_link`, `open_the_link_through_the_launcher`

Definition of done:

- [ ] The failing tests listed above were written first and now pass
- [ ] `dotnet build` finishes with 0 errors and 0 warnings
- [ ] Every test assembly passes, including
  `AStarDev.CloudSync.TestsArchitecture`
- [ ] XML docs check is clean (see the Gate line)
- [ ] No `//` comments, no underscore fields, no stray files
- [ ] No unrelated files changed
- [ ] PRD criteria covered by this slice are ticked: PRD 5 (UI)
- [ ] The PR description reports evidence (command output, test counts), not
  only a claim that it works

### C11: Architecture sweep: every error has a code and link

An architecture test fails when an error case or custom exception type has no
code or link, or two share a code.

PRD requirements covered: PRD 6.

Scope:

- Reflection over every error union case and custom exception type in the app
  assemblies
- Codes unique across the solution

Out of scope:

- BCL exceptions thrown at a site are covered by that site's own test (C3 to
  C8); the sweep cannot see them

Acceptance criteria:

- AC1: A new error case without a code fails the test
- AC2: A duplicate code fails the test

Blocked by: C2, C3, C4, C5, C6, C7, C8. Wave 4. Parallel with: C9, C10.
Estimated files: 8.

Conflict hotspots: None.

Verification:

- AC1: A new error case without a code fails the test. Check: test
  `ErrorsArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- AC2: A duplicate code fails the test. Check: test
  `ErrorsArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`; run `dotnet exec
  apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.TestsArchitecture/bin/Debug/net11.0/AStarDev.CloudSync.TestsArchitecture.dll`.
- Gate: `dotnet build` (0 errors, 0 warnings), every `*TestsUnit` assembly and
  `AStarDev.CloudSync.TestsArchitecture`, and `dotnet build
  -p:GenerateDocumentationFile=true` with no CS1591, CS1572, CS1573 or CS1574 in
  production projects.

Tests to write first:

- `ErrorsArchitectureTestsShouldEnsure` in
  `AStarDev.CloudSync.TestsArchitecture`:
  `give_every_error_case_a_code_and_link`, `use_each_code_once`

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

## Epic acceptance

- [ ] Every case of every error union returns a non-empty code and a link under
  the documented root (slice C2 to C8, C11)
- [ ] A thrown exception's HelpLink equals its link (slice C3, C5, C6, C7, C8)
- [ ] A UI error shows its code and link; a log line for it contains the code
  (slice C9, C10)
- [ ] A new error case without a code fails an architecture test (slice C11)
- [ ] Build has 0 warnings; every test assembly passes (slice every slice)

Independent verifier prompt:

Check each criterion in the epic acceptance checklist against the merged code.
Report pass or fail per criterion with evidence (test names, command output,
file and line). Change nothing.

## Assumptions and decisions

- Interview 2026-11-22: root `https://astar-development.co.uk/errors/<code>`;
  codes `CS-<MODULE>-<NNNN>`; both thrown exceptions and Result error cases;
  shown in logs, the IPC reply and the UI error panel; own project
  AStarDev.CloudSync.Errors.
- Defaults taken: C3 and C10 follow A9 because all three change the IPC failure
  shape; C9 follows Epic B so it edits generated log messages only.
- Limit: the architecture sweep (C11) sees union cases and custom exception
  types, not a BCL exception thrown at a site; those are covered by the site's
  own test.
- File counts are estimates.

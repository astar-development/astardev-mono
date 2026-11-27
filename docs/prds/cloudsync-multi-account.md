# CloudSync multi-account

## Problem

Docs 24 promises multiple accounts with independent health. The code serves one: a process-wide `AccessTokenHolder` and one `RootEnumerator`/`ItemTransfer` carry the token of the latest sign-in; `SyncJobStore` allows a single job; `ScopeSelectionViewModel` takes `Jobs[0]` and `Accounts[0]`; `GetRemoteFolders`, `GetRemoteRootSummary` and `GetDriveInfo` carry no account. Adding account 2 (2026-11-26) switched account 1's running job to account 2's token, added account 2's folders as scopes of account 1's job, and stored account 2's delta link in account 1's `ProviderState`. Result: Graph 400 on every delta call.

## Rules

1. One account owns any number of jobs; a job, its scopes, delta state and operations belong to exactly one account.
2. Every provider call that touches a drive names the account. A call never uses another account's token.
3. Folder names may repeat across accounts (both have `Desktop`); nothing keys on a folder name alone.
4. Disconnecting an account removes only that account's tokens and data.
5. Local roots of two jobs should not overlap. Not enforced yet; the add journey proposes a root per account.

## Slices

| # | Slice | Done when |
|---|-------|-----------|
| S1 | Port and OneDrive: every `ICloudProvider` drive method takes `accountIdentifier` first; OneDrive keeps one token holder, `RootEnumerator` and `ItemTransfer` per account; disconnect removes one account from the MSAL cache | two accounts signed in, each call carries its own account's token (unit test) |
| S2 | Sync engine: `DeltaProcessingService`, `InitialCrawler`, `OperationExecutor` resolve the job's account identifier and pass it | each job syncs with its own account (test with two jobs on the fake provider) |
| S3 | Persistence: job store allows one job per account and rejects a second job for the same account | `SyncJobAlreadyExists` becomes per account |
| S4 | IPC and daemon: `GetRemoteFolders`, `GetRemoteRootSummary`, `GetDriveInfo` carry `AccountId`; `AddSyncScope` checks the folder belongs to the job's account; startup runs every job | scopes of account 2 land on account 2's job |
| S5 | UI: add journey creates a job for the chosen account; scope screen picks the job by account; no `Jobs[0]`/`Accounts[0]` | two accounts, same-named folders, no crash |
| S6 | Docs: update PRD mvp rule "one account, one job", docs 07 and 24 | docs match code |

## Data repair

Existing `cloudsync.db`: remove scopes of drive `A80AA3A6B0ECD59E` from job `D3620BD2`, clear that job's `ProviderState`, keep a backup. One-off by hand; no migration.

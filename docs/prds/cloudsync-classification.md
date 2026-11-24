# File classifications

Source: owner request, 2026-11-24 (no idea doc). Status: Draft. Date:
2026-11-24.

## Problem

Synced files carry meaning in their extension, folder names and the words in
their path, but CloudSync cannot group or browse by it. The earlier OneDrive
sync client (`astar-dev-mono`) already classifies files into categories and
terms; this CloudSync has no equivalent. One account already holds about 700,000
files, so the design must stay cheap at that size.

## Goals

- Every synced file is classified automatically into categories and terms, and
  the links stay current as files are added, moved and removed.
- The user can browse terms, see the files behind a term, assign a term to a
  file, and promote or ignore suggested words.
- A full reclassify of the index can be run and shows progress, without stalling
  sync.

## Non-goals

- A second database file. Revisit with `ATTACH` only if sync stalls during a
  reclassify are measured.
- Content-based classification (reading file bytes).
- Changing sync, conflict or reconciliation behaviour.
- Carrying the old `Accounts` or `Sync.Sqlite` types over.

## Current state

Old implementation: `Classification` (about 2.8k lines: tokeniser,
`PhraseMatcher`, `ExtensionSignal`, `FolderSegmentSignal`,
`DictionaryTermSignal`, `ClassificationCombiner`, suggestions, seeder,
reclassifier, embedded `Seed/*.json` and `Lists/*.txt`), `Classification.Sqlite`
(own DbContext, two migrations) and `ViewModels/Classification` (tabs, term
list, suggestions).

This repo: no classification code. Items are identified by `ItemId` (a `Guid`)
in `ItemEntity`, written through `IItemIndexStore` (`SavePageAsync`,
`ApplyDeltaPageAsync`). Persistence is one `CloudSyncDbContext`. The UI has a
rail of screens (`ScreenIds`) and an IPC project of commands and views.

## Requirements

1. Must: a new module `AStarDev.CloudSync.Classification` holds the pure engine
   (tokeniser, phrase matching, signals, combiner, suggestion scanning, seed
   parsing). It does no I/O and references neither Persistence nor Avalonia.
1. Must: failable contracts return `Result` with a closed error union; no
   throwing, no `null`, every async method takes a last `CancellationToken`.
1. Must: categories, terms, file links, seed state and reclassify runs are
   tables in `CloudSyncDbContext`, added by one migration.
1. Must: a link row is keyed by `ItemId` and term id, never by path string, and
   carries its origin (automatic or user). Deleting an item deletes its links.
1. Must: indexes serve both `(ItemId, TermId)` and `(TermId, ItemId)` lookups.
1. Must: bulk link writes are batched, 1,000 to 5,000 rows per transaction.
1. Must: the seed (categories, terms, extension and folder lists, word lists)
   ships as embedded resources and is applied once per seed version.
1. Must: files written by a crawl or delta page are classified after the index
   write; a classification failure is logged and never fails the sync.
1. Must: automatic replacement never removes a user link or restores an ignored
   term.
1. Must: a reclassify run recomputes automatic links for every file, reports
   progress after each batch, is cancellable, and is safe to rerun.
1. Must: the daemon applies the seed at startup and reclassifies once when the
   seed is newly applied or no run was ever recorded.
1. Must: the daemon serves term list, files for a term, assign term,
   suggestions, promote, ignore, reclassify start and reclassify progress over
   IPC.
1. Must: a Classifications screen with Terms and Suggestions tabs and a
   reclassify control, reached from the rail.
1. Must: the new architecture doc, updates to docs 11 and 15, a Classification
   event-id block, and public surface test entries land with the code.
1. Should: classification log messages use `[LoggerMessage]` in a
   `ClassificationLog` class.

## Acceptance criteria

- Crawling a fake provider tree links each file to the terms its extension,
  folders and path words imply (req 1, 8).
- Removing an item removes its links; a user link survives a reclassify (req 4,
  9).
- Reclassifying 700,000 index rows completes in batches while a sync pass
  interleaves its writes (req 6, 10).
- A fresh database gains the seed once; restarting does not reapply it (req 7,
  11).
- Cancelling a reclassify stops it and a rerun gives the same links (req 10).
- The UI lists terms with file counts, shows a term's files, and promote or
  ignore moves a suggestion (req 12, 13).
- `dotnet build` is clean, all `*TestsUnit` and the architecture tests pass (req
  14).

## Constraints and decisions

Decided 2026-11-24 with the owner:

- Engine lives in a new `AStarDev.CloudSync.Classification` module, not in
  Domain or SyncEngine.
- Full parity with the old client, delivered as an epic of ordered slices.
- One SQLite file and one DbContext; no second database.
- Documented sizing: about 700,000 files times about 5 links is 3.5M rows.
- The old IDs were path-keyed integers; here `ItemId` is a `Guid`, so links key
  on `ItemId` as stored by `StronglyTypedIdConverters`.
- Docs are the source of truth: the new classification doc is written first.

## Risks

- A reclassify holds the single SQLite writer; mitigate with small batches and
  throttling, and measure before considering a second file.
- The seed `place.json` is about 2 MB; loading it must not slow daemon start.
- A Guid key makes link rows larger than integer keys; check index size at 3.5M
  rows.

## Open questions

- Can the user edit categories, or only terms? Default: terms only, categories
  come from the seed.
- Does the UI show classifications per account or across all accounts? Default:
  across all accounts, with the account named per file.

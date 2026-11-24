# 25 File Classification

Automatic grouping of synced files by extension, folder names and the words in
their path.

## Purpose

Synced files carry meaning in their extension, folders and path words.
Classification links each file to terms, and each term to a category, so the
user can browse and group files without reading paths.

## Goals

- Every synced file is classified automatically and kept current.
- The user can assign, promote and ignore terms, and manage categories and
  terms.
- A reclassify of the whole index is safe to run and rerun, and never stalls
  sync.

## Non-Goals

- Content-based classification.
- A second database file.
- Any change to reconciliation or sync decisions.

## Module

```text
AStarDev.CloudSync.Classification
```

Contains the pure engine: tokeniser, phrase matching, signals, combiner,
suggestion scanning, seed parsing, the vocabulary provider and the reclassify
logic. It does no I/O of its own: storage goes through ports.

Dependencies:

```text
Domain
Ports
Errors
AStarDev.FunctionalParadigm
```

Must Not Contain:

```text
EF Core
Avalonia
Sync Decisions
```

Only the Daemon references Classification, to compose it. Persistence
implements the classification ports, so Persistence never references
Classification, and neither do SyncEngine, IPC, UI, OneDrive or Observability.

## Where Things Live

| Concern | Module |
|---------|--------|
| `CategoryId`, `TermId` | Domain |
| Store ports and `ClassificationError` | Ports |
| Engine, seed, reclassify, sync hook | Classification |
| Tables, stores, migration | Persistence |
| Commands and views | IPC |
| Startup and handlers | Daemon |
| Classifications screen | UI |

## Data Model

- A category groups terms and comes from the seed or the user.
- A term belongs to one category and has a status: suggested, active or
  ignored.
- A file link joins an item to a term. It records its origin: automatic or
  user. Automatic replacement never removes a user link.
- Links key on `ItemId`, never on the path, so a rename or move keeps them and
  deleting an item deletes its links.

## Storage

Classification tables live in the one `CloudSyncDbContext` and the one SQLite
file, added by one migration. Link indexes serve both lookups: by item
(`ItemId`, `TermId`) and by term (`TermId`, `ItemId`).

A second file is not used. SQLite allows one writer, so bulk writes are
batched at 1,000 to 5,000 rows per transaction and a reclassify yields between
batches. A second file attached with `ATTACH` is reconsidered only if sync
stalls during a reclassify are measured.

## Seed

The shipped categories and terms are embedded JSON files with a version. The
daemon applies them in startup stage 2, once per version. Terms already stored
are left as they are, so a term the user ignored or edited is never restored by
a newer seed. Terms are written in transactions of at most 1,000.

## Vocabulary

The vocabulary provider loads the categories and terms once and keeps them
until it is invalidated. The term and category stores are wrapped so any
successful write, from the seed or from the user, invalidates it. Ignored terms are dropped when it
is built, so no signal can emit one.

## Sync Hook

Files written by a crawl or delta page are classified after the index write,
by a decorator that wraps the item index the Persistence module registers.
The links are written by the file's provider identity, because the page report
carries no item identity. A deleted item loses its links with the item.
A classification failure is logged and never fails the sync. When the
vocabulary changes while a batch is classified, the batch is classified again.

## Suggestions

A scan of every stored file path finds words that are not yet terms and ranks
them by the number of files containing them. A word that is a term in any
status, or a common English word, is left out. A first name carries a hint,
and is never assigned automatically. The user can promote a word to an active
user term or ignore it; an ignored word is kept, never deleted, so it never
comes back. Files are read a page at a time, so a scan never holds the whole
index in memory.

## Reclassify

A reclassify recomputes the automatic links of every file a page at a time,
reports progress after each page, honours cancellation and gives the same
result when rerun. It pauses briefly after each page so sync writes get the
single SQLite writer between pages, and starts over when the vocabulary changes
during the run, up to three passes. User links and ignored terms are never
touched.

The last finished run is remembered with the seed version applied at the time.
After startup has finished, a background service starts a run when none was
ever recorded or a newer seed was applied since, so a long run never delays the
daemon accepting connections. It is skipped on a database in recovery mode. A
run that was cancelled or failed is not recorded, so the next start tries
again.

## Screen

The Classifications screen is a primary destination (documents 18 and 19). It
lists every term with its category, where it came from and its file count,
filtered by text and ordered by term, category or file count. Selecting a term
lists its files a page at a time, each with the account that holds it. The UI
reads and edits only through IPC; the daemon counts and pages.

A Suggestions tab lists the unreviewed words, scanned the first time the tab is
shown, with a category picker and a promote and an ignore button per word. Forms
below the terms add, rename and remove categories and add, edit and remove
terms; the daemon words every refusal and the form shows it and keeps what was
typed. A reclassify button starts a run and shows its progress from the
daemon's events, also for a run the daemon started at startup. Any accepted
change, and the end of a run, makes the screen read the terms again.

## Logging

Classification log messages use the 9000 block of event ids, in a
`ClassificationLog` class.

## Related Documents

- 07 Daemon and UI IPC contract: classification commands and views.
- 11 Solution structure and module boundaries.
- 14 Solution startup and composition: where the seed and reclassify start.
- 15 Architecture testing and governance.
- 18 and 19: the Classifications screen.

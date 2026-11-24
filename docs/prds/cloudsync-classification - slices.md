# File classifications: slices

PRD: [cloudsync-classification.md](<cloudsync-classification.md>). Status: Draft
plan. Date: 2026-11-24.

## Epic

Title: `feat(cloudsync): file classifications`.

Port file classification from the old OneDrive sync client into CloudSync as a
new `AStarDev.CloudSync.Classification` module, one `CloudSyncDbContext`
migration, daemon startup, sync hook, reclassify run, IPC and a Classifications
screen.

Goals: every synced file is classified and kept current; the user can browse
terms and categories, assign terms, promote or ignore suggestions and run a
reclassify with progress.

Out of scope: second database file, content-based classification, sync behaviour
changes.

Definition of done: all slices merged, epic acceptance checklist ticked with
evidence.

## Order

| Wave | Slice | Blocked by | Parallel with | Est. files |
|------|-------|------------|---------------|------------|
| 1 | S1 | none | none | 14 |
| 2 | S2 | S1 | S3 | 22 |
| 2 | S3 | S1 | S2 | 24 |
| 3 | S4 | S2, S3 | none | 18 |
| 4 | S5 | S4 | S6, S7 | 10 |
| 4 | S6 | S4 | S5, S7 | 20 |
| 4 | S7 | S4 | S5, S6 | 12 |
| 5 | S8 | S5, S6, S7 | none | 18 |
| 6 | S9 | S8 | S10 | 16 |
| 6 | S10 | S8 | S9 | 22 |
| 7 | S11 | S9, S10 | none | 22 |

Critical path: S1 then S2 then S4 then S6 then S8 then S10 then S11.

Wave 4 slices all register services in the daemon composition method; expect a
trivial merge conflict in `DaemonServiceCollectionExtensions.cs`, rebased by
whichever slice merges second.

## Slices

### S1: Architecture doc and module skeleton

Enabling slice: every later slice needs the module, its test project, the rules
and the event-id block.

Covers: PRD requirement 14 (docs and rules part), 15 (log class).

Scope in: new doc `25 cloudsync-classification.md`; updates to docs 11 and 15
and the index; new projects `AStarDev.CloudSync.Classification` and
`AStarDev.CloudSync.Classification.TestsUnit` added to `AStarDev.slnx`; id types
`CategoryId` and `TermId` in Domain; log event-id block 9000 in the
observability doc and `LogEventIdsArchitectureTestsShouldEnsure`; dependency
rule: Classification references Domain and Ports only and not Persistence or
Avalonia. Scope out: any engine code.

Files (about 14): the doc, docs 11, 15, 00, `AStarDev.slnx`, two csproj files,
Domain id types, architecture tests.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Build clean | Gate | `dotnet build` |
| Dependency rule enforced | Test | `ClassificationArchitectureTestsShouldEnsure` in `TestsArchitecture` |
| Event block 9000 enforced | Test | `LogEventIdsArchitectureTestsShouldEnsure` |

Tests first:
`ClassificationArchitectureTestsShouldEnsure.not_reference_persistence_or_avalonia`
(fails until the module exists).

### S2: Classification engine

Covers: PRD requirements 1, 2, 9 (combiner honours ignored terms).

Scope in: port the pure logic: tokeniser, `PhraseMatcher`, `PhraseIndex`,
`ClassificationText`, `ExtensionSignal`, `FolderSegmentSignal`,
`DictionaryTermSignal`, `ClassificationCombiner`, `FileClassifier`, vocabulary
and candidate types, confidence. Result and error unions per `CLAUDE.md`, no
`null`, no throwing. Scope out: stores, seed files, vocabulary provider.

Files (about 22): the ported types, their tests, public surface test entry.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Path classified by extension, folder, word | Test | `FileClassifierShould.link_a_file_to_terms_from_extension_folder_and_words` |
| Ignored term dropped | Test | `ClassificationCombinerShould.drop_ignored_terms` |
| High beats Low | Test | `ClassificationCombinerShould.prefer_high_confidence_for_the_same_term` |
| No candidates is unclassified | Test | `ClassificationCombinerShould.return_unclassified_when_there_are_no_candidates` |

All in `Classification.TestsUnit`. Tests first: the four above.

### S3: Persistence and ports

Covers: PRD requirements 3, 4, 5, 6, 9 (user links survive replace).

Scope in: ports `ICategoryStore`, `ITermStore`, `IFileClassificationStore`
(including category and term create, rename, delete) in Ports with a closed
`ClassificationError` union; entities and configuration for categories, terms,
file links (origin column, `ItemId` key, cascade delete), seed state and
reclassify runs in `CloudSyncDbContext`; one migration; the stores; batched
`ReplaceAutoLinks` of 1,000 to 5,000 rows per transaction; indexes `(ItemId,
TermId)` and `(TermId, ItemId)`. Scope out: seeding, sync hook.

Files (about 24): entities, configurations, stores, migration plus designer and
snapshot, port interfaces, errors, tests. Hotspot: `CloudSyncDbContext.cs` and
the model snapshot.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| One migration, no empty migration | Gate | migration review plus build |
| Links keyed by item, cascade on delete | Test | `FileClassificationStoreShould.remove_links_when_the_item_is_removed` |
| User link survives replace | Test | `FileClassificationStoreShould.keep_user_links_when_replacing_automatic_links` |
| Batch writes | Test | `FileClassificationStoreShould.write_large_updates_in_batches` |
| Term-first lookup | Test | `FileClassificationStoreShould.return_the_files_for_a_term` |

All in `Persistence.TestsUnit`. Tests first: the four tests above.

### S4: Seed and vocabulary

Covers: PRD requirements 7, 11 (seed part), 15.

Scope in: embedded `Seed/*.json` and `Lists/*.txt` copied verbatim from the old
client; seed source and seeder with seed-version state; vocabulary provider with
cache and generation counter; daemon startup step applying the seed once,
registered in the daemon composition method. Scope out: reclassify after seed
(S6).

Files (about 18): resources, seeder, provider, startup, composition, tests.
Hotspot: `DaemonServiceCollectionExtensions.cs`.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Seed applied once | Test | `ClassificationSeederShould.apply_the_seed_once_per_version` |
| Restart does not reapply | Test | `ClassificationSeederShould.report_already_current_on_a_second_run` |
| Startup wiring | Test | `DaemonServiceCollectionExtensionsShould.apply_the_classification_seed_at_startup` in `Daemon.TestsUnit` |
| Startup stays fast | Manual | start the daemon on a clean database, note seed time in the PR |

Tests first: the three tests above.

### S5: Classify on sync

Covers: PRD requirements 8, 9 (hook part).

Scope in: classify each file written by `SavePageAsync` and
`ApplyDeltaPageAsync` after the index write, retrying when the vocabulary
generation changes; failure is logged and never fails the sync; removed items
lose their links. Scope out: reclassify.

Files (about 10): hook, composition, tests. Hotspot: `ItemIndexStore.cs`.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Crawled files get links | Test | `ClassifyingItemIndexStoreShould.link_files_after_a_page_is_saved` |
| Failure does not fail sync | Test | `ClassifyingItemIndexStoreShould.return_the_inner_result_when_classification_fails` |
| Removed item unlinked | Test | `ClassifyingItemIndexStoreShould.remove_links_for_removed_items` |
| End to end with fake provider | Test | `ClassificationShould.classify_a_crawled_tree` in `TestsIntegration` |

Tests first: the four above.

### S6: Reclassify run

Covers: PRD requirements 10, 11 (reclassify part), 2.

Scope in: reclassifier over the whole index in batches with progress per batch,
cancellation and safe rerun; run store and freshness check; startup reclassify
after a newly applied seed or when no run was recorded; throttling so sync
writes interleave. Scope out: IPC.

Files (about 20): reclassifier, progress types, run store, freshness, startup
logic, tests.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Progress after each batch | Test | `ClassificationReclassifierShould.report_progress_after_every_batch` |
| Cancel stops, rerun gives same links | Test | `ClassificationReclassifierShould.stop_on_cancellation_and_give_the_same_links_on_rerun` |
| Second start while running | Test | `ClassificationReclassifierShould.return_already_running_for_a_second_start` |
| Startup runs once after seed | Test | `ClassificationStartupShould.reclassify_once_after_a_new_seed` |
| Sync interleaves at scale | Manual | reclassify the 700,000-file account while a sync runs; note any sync stall in the PR |

Tests first: the four tests above.

### S7: Suggestions

Covers: PRD requirements 9, 12 (suggestion part).

Scope in: suggestion scan over file paths (ranked by file count, then
alphabetical, excluding terms already known and word lists), promote and ignore
operations that are repeat-safe and parallel-safe. Scope out: IPC and UI.

Files (about 12): scanner, hint and suggestion types, tests.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Ranked list | Test | `ClassificationSuggestionsShould.rank_by_file_count_then_alphabetically` |
| Promote repeat-safe | Test | `ClassificationSuggestionsShould.give_one_term_for_parallel_promotes` |
| Ignore never returns | Test | `ClassificationSuggestionsShould.not_suggest_an_ignored_word_again` |

Tests first: the three above.

### S8: IPC read side

Covers: PRD requirement 12 (read, reclassify).

Scope in: IPC commands and views for term list with file counts, files for a
term (account named), suggestions, reclassify start and progress events; daemon
query and handler classes; doc 07 update. Scope out: writes and UI.

Files (about 18): IPC records, daemon queries, doc 07, tests. Hotspots: IPC
envelope registration, `DaemonServiceCollectionExtensions.cs`.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Term list over IPC | Test | `TermsQueryShould.return_terms_with_file_counts` in `Daemon.TestsUnit` |
| Files for a term | Test | `TermFilesQueryShould.name_the_account_for_each_file` |
| Progress event | Test | `ReclassifyProgressSinkShould.publish_progress_over_ipc` |
| Envelope round trip | Test | `ClassificationEnvelopesShould.round_trip` in `IPC.TestsUnit` |

Tests first: the four above.

### S9: IPC write side

Covers: PRD requirement 12 (writes, category editing).

Scope in: commands for assign term to file, promote, ignore, create, rename and
delete category, create, rename and delete term, and their handlers returning
closed errors. Scope out: UI.

Files (about 16): IPC records, handlers, tests.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Assign term | Test | `AssignTermHandlerShould.link_the_term_as_a_user_link` |
| Delete category with terms | Test | `DeleteCategoryHandlerShould.refuse_a_category_that_has_terms` |
| Promote and ignore | Test | `PromoteSuggestionHandlerShould.make_the_word_a_user_term` |

All in `Daemon.TestsUnit`. Tests first: the three above.

### S10: Classifications screen and terms

Covers: PRD requirement 13 (terms part), 15.

Scope in: `ScreenId.Classifications` in rail and nav (docs 18, 19), view model
and view with terms tab, sortable list with file counts, files for a term,
localisation strings, themes. Scope out: suggestions tab, reclassify control,
category editing.

Files (about 22): screen id, view model, view, tabs, strings, tests. Hotspots:
`ScreenIds.cs`, localisation `Strings/` files.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Screen reachable | Test | `ScreenIdsShould.include_classifications_in_the_rail` in `UI.TestsUnit` |
| Terms listed with counts | Test | `ClassificationTermListViewModelShould.load_terms_with_file_counts` |
| Files for a term | Test | `ClassificationTermListViewModelShould.show_the_files_for_the_selected_term` |
| Looks right in light and dark | Manual | open the screen in both themes; terms and counts readable |

Tests first: the three tests above.

### S11: Suggestions, reclassify and category editing in the UI

Covers: PRD requirements 13 (rest), 12 (UI use).

Scope in: suggestions tab with promote and ignore, reclassify control with
progress, category and term editing forms, localisation strings. Scope out:
backend changes.

Files (about 22): view models, views, strings, tests.

Verification:

| Criterion | Check | Where |
|-----------|-------|-------|
| Promote moves a suggestion | Test | `ClassificationSuggestionsViewModelShould.remove_a_promoted_word_from_the_list` |
| Reclassify shows progress | Test | `ReclassifyViewModelShould.show_progress_until_done` |
| Category editing | Test | `CategoryEditorViewModelShould.create_rename_and_delete_a_category` |
| Whole flow | Manual | run the daemon and UI, promote a word, reclassify, see the term count change |

Tests first: the three tests above.

## Epic acceptance

- [ ] Crawl links each file from extension, folders, words (S2, S5).
- [ ] Removed item removes links; user link survives reclassify (S3, S5, S6).
- [ ] 700,000-row reclassify in batches, sync interleaves (S6 manual).
- [ ] Seed applied once, not on restart (S4).
- [ ] Cancel stops a reclassify; rerun gives same links (S6).
- [ ] UI lists terms, shows files, promote or ignore works (S10, S11).
- [ ] Build clean, all `*TestsUnit` and architecture tests pass (every slice).

Independent verifier prompt: "Check each criterion in the checklist against the
merged code. Report pass or fail per criterion with evidence. Change nothing."

## Assumptions and decisions

- Interview 2026-11-24: categories are editable (adds create, rename and delete
  in S3, S9, S11); classifications span all accounts; seed copied verbatim;
  backend first, UI last.
- File counts are estimates.
- Links key on `ItemId` (a `Guid`), not an integer as first suggested.
- Event-id block 9000 assumed for Classification; S1 confirms.
- The PRD open question on category editing is now decided; the PRD still lists
  it as open and should be updated.

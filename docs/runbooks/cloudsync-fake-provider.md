# Run the daemon against the fake provider

Lets you crash the daemon with a real `SIGKILL` and watch it recover, with no OneDrive account and a generated tree.
Design: `docs/architecture/14 solution-startup-and-composition.md` (Test Composition). Debug builds only: a release build has no fake provider and ignores these settings.

## Start the daemon on the fake tree

```bash
CloudSync__Provider=Fake CloudSync__FakeTreeSize=700000 dotnet run --project apps/desktop/AStarDev.CloudSync/AStarDev.CloudSync.Daemon
```

- `CloudSync__Provider`: `Fake` or `OneDrive` (the default).
- `CloudSync__FakeTreeSize`: items in the generated tree; 700000 when unset. Every tenth item is a folder, the rest are small files whose content is their own id.
- The tree is generated in memory with fixed ids, so a restarted daemon serves the same tree and the crawl resume tokens stay valid.
- Use a throwaway database so the run does not touch real state: add `Database__Path=/tmp/fake-cloudsync.db`. Delete it to start from scratch.

Connect an account, create a sync job with an empty local folder, select the root and start the sync from the UI (the fake connects at once). The crawl starts, then the downloads.

## Check 1: kill mid-crawl

1. Start the sync and watch the daemon log until the crawl is part way (page-by-page progress).
2. `kill -9 $(pgrep -f AStarDev.CloudSync.Daemon)`.
3. Start the daemon again with the same settings and the same `Database__Path`.
4. Expect: the crawl resumes from its checkpoint instead of starting over, and finishes with every item indexed once.

## Check 2: kill mid-operation

1. Let the crawl finish and the downloads start (queue depth in the log or the dashboard).
2. `kill -9 $(pgrep -f AStarDev.CloudSync.Daemon)`.
3. Start the daemon again.
4. Expect: the queue recovery stage logs that interrupted operations were requeued, each interrupted operation runs once more, and no operation is lost or duplicated.

A smaller tree (for example `CloudSync__FakeTreeSize=20000`) reaches the download phase sooner.

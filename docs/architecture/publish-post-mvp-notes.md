# Publish post-MVP notes

Status: notes from a review on 2026-11-24, kept so the decisions can be picked up later. Nothing here is implemented, and nothing here changes the architecture docs. Where a statement was not verified it says so.

Context: the PRD (`docs/prds/cloudsync-mvp.md`) scopes the MVP as Linux only, with "Release automation, upgrade strategy" and "Windows support, named-pipe transport" as non-goals. This note records what shipping the app (AppImage, auto-update) and supporting other platforms would involve, based on the old app's publishing in `astardev-mono`.

## 1. What the old repo does today

Reviewed in `astardev-mono` (`.github/workflows`, `.github/desktop-apps.json`, `.github/scripts`, `packages/core/utilities`).

### Release pipeline

- `desktop-release-on-merge.yml` runs on every push to `main`:
  1. finds the apps whose directories (listed per app in `.github/desktop-apps.json`) the push touched;
  2. computes each app's next version from its latest stable `<tag_prefix>X.Y.Z` tag and the pushed commit messages (`feat` is minor, anything else patch, `!` or a `BREAKING CHANGE:` footer is major);
  3. pushes the tag and calls `desktop-app-release.yml`.
- An app with no tag yet starts at `0.1.0` (`compute_next_desktop_version.py`, `FIRST_VERSION`). That already matches the wish to start below 1.0.
- A breaking change bumps major, so the first `!` commit would jump to `1.0.0`. See the questions below.
- `desktop-app-release.yml` builds, tests, runs `dotnet publish --self-contained`, packs with `vpk pack` and uploads with `vpk upload github --merge`.
  - Linux runs first and is the only job that can fail the workflow. Windows and macOS are best-effort (`continue-on-error`).
  - Each platform has its own Velopack channel (`<channel_prefix>-linux`, `-win`, `-osx`), because Velopack's update lookup is scoped by channel across the whole repo, not by pack id.
  - Release notes come from the tag annotation.
  - Delta updates are off for CloudSync.
  - It injects an Application Insights connection string, which the new app has no use for.
- The old workflow's manual-dispatch list does not include CloudSync. The workflow sets up .NET 10; this repo is .NET 11.
- The scripts under `.github/scripts` (and their tests) are needed by the workflow and would have to be copied.
- The old repo pins `vpk` 1.2.0 while the libraries use Velopack 1.2.161. This was not investigated.
- Every merge that touches an app's directory releases that app, documentation and tests included.

### The two NuGet packages

| Package | Versions on NuGet.org | Purpose |
|---|---|---|
| `AStarDev.VelopackPublishing` | 1.3.1 | Wraps Velopack's `UpdateManager`. Reads `Updates:GithubRepositoryUrl` and `Updates:ChannelPrefix`; channel is `{ChannelPrefix}-{platform}`. Adds an update policy, a JSON deferral store and `IUpdateApplyGate`. Namespace `AStar.Dev.Velopack.Publishing`. |
| `AStarDev.VelopackPublishingAvalonia` | 0.1.0, 1.2.1 | The "update available" dialog (Restart now / Later), its view model, a scheduler and notification service. Needs 8 theme brush keys (`BackgroundPrimaryBrush`, `TextPrimaryBrush`, and so on) and an `IUpdateDialogTextProvider` (depends on `AStarDev.Localization` 0.1.0). |

### Problems found with reusing them as published

- **Binary incompatible with this repo's `AStarDev.FunctionalParadigm`.** Both packages were built against the published 0.1.4 package (the old `Exceptional<T>` class). This repo's `nuget-packages/AStarDev.FunctionalParadigm` has the same assembly name and a different API. Comparing the member references in the package DLLs against this repo's assembly found `Unit.Instance` missing, and the `Try.Run`, `Try.RunAsync`, `Match` and `MatchAsync` signatures differ. NuGet would use this repo's project, so a `MissingMemberException` at runtime is expected. Not run, so the exact failing code path is unconfirmed.
- **Heavy transitive dependencies:** `AStarDev.LoggingExtensions` (with LoggingOTel and Utilities), Azure Monitor and OpenTelemetry exporters, MSAL and System.Reactive. Both packages target net10.0.

### Velopack on Linux

From the Velopack documentation:

- There is no installer on Linux. The program is a self-updating `.AppImage`.
- The AppImage is one read-only file, mounted temporarily when run. Updates replace the whole file atomically.
- Anything that must persist (settings, logs, the database) has to live outside it.

## 2. Is CloudSync cross-platform, and is it publishable?

### Cross-platform

Not today, by documented decision. The structure allows it.

- Platform-neutral: Domain, SyncEngine, SQLite persistence and the Graph/OneDrive code. Not audited for Windows path rules (case-insensitive paths, reserved names, separators).
- Linux-only parts, each behind an interface, so another OS means writing adapters:

| Concern | Interface | Linux implementation | What another OS needs |
|---|---|---|---|
| IPC transport | `IIpcServer` / `IIpcClient` | Unix domain socket | Doc 07 names named pipes for Windows |
| Token storage | `ITokenCacheStore` | `secret-tool` (libsecret, D-Bus session) | Keychain / DPAPI-backed cache |
| Local file identity | `ILocalIdentityReader` | libc `stat`, guarded by `OperatingSystem.IsLinux()` | File IDs on NTFS, inode on macOS |
| Desktop notifications | `IDesktopNotifier` | `notify-send` | Native toast / notification centre |
| Paths and service | none | XDG directories, systemd user unit | Platform directories, a service mechanism |

### Publishable

Not yet, with nothing found that blocks it.

- Verified: a self-contained `linux-x64` publish of the daemon and the UI into one folder works (about 118 MB, 300 files), and the published daemon starts and runs its startup stages (the first four of six were checked).
- Not verified: the published UI, a sign-in from a published build, behaviour on Windows paths.
- Gaps:
  1. **No shipped configuration.** `OneDrive:ClientId` comes from configuration and is deliberately not committed. The publish output has no `appsettings`, so a downloaded build could not sign in. A public client id is not secret, so the release build can inject it.
  2. **Linux prerequisites.** `secret-tool` and `notify-send` must be installed by the user; an AppImage does not bundle them.
  3. **Accounts.** The Azure registration is personal Microsoft accounts only (`consumers` authority); business accounts are out of scope in the PRD. Relevant if the app is to be sold.
  4. **Packaging.** The two-process design plus a systemd unit does not fit an AppImage as is (see question 1). The "Stop/Restart daemon" setting assumes the daemon runs under the systemd unit.
  5. **Velopack packages.** The compatibility problem above.
  6. **Licence.** The repository is MIT, so sharing is fine. Nothing supports selling it (licence checks, EULA).

## 3. Open questions

Raised during the review and not yet answered.

1. **How does the daemon ship inside an AppImage?** A systemd unit cannot point at a path inside a temporarily mounted file, and a daemon cannot reliably outlive the UI's mount.
   - Recommended: one AppImage containing both binaries. The UI starts the daemon itself and restarts it after an update; before applying an update it stops the daemon through the existing `StopDaemon` command. The systemd unit stays as the advanced, non-AppImage route.
   - Alternative: a separate install for the daemon.
2. **How do the Velopack packages get into this repo?**
   - (a) Copy both projects' sources into `nuget-packages/`, adapted to this repo's FunctionalParadigm and conventions (recommended).
   - (b) Publish this repo's FunctionalParadigm as a new version to NuGet and rebuild both old-repo packages against it.
   - (c) Write a small update service here using only the `Velopack` library.
3. **Linux only?** The daemon uses Unix sockets and `secret-tool`, so Windows and macOS jobs would fail. Recommendation: drop them from the pipeline.
4. **Versioning.**
   - Keep major at 0 until 1.0.0 is released by hand (the old script would turn the first breaking change into 1.0.0).
   - Should every merge touching `apps/desktop/AStarDev.CloudSync/` release, or only changes to source projects?
5. **Pack id.** Keep `AStarDev.CloudSync`, the same as the old app's pack id? The feeds are separate repositories, so nothing collides, but it could confuse anyone who has the old app installed.

## 4. Suggested order when this is picked up

1. Decide the first shipping target (suggested: Linux AppImage).
2. Inject the OneDrive client id at release time.
3. Decide the daemon packaging (question 1) and the update plumbing (question 2).
4. Add the release workflow and scripts, with the version policy from question 4.
5. Record in the docs which adapters Windows and macOS would need, without building them.

# 19 Navigation and Screen Architecture

## Purpose

Defines the navigation model, screen architecture and window behaviour for CloudSync, supporting Correctness, Observability, Recoverability and User Trust.

## Design Principles

Navigation exists to support confidence and trust rather than feature discovery. Users can always understand what CloudSync is doing, why, and whether action is required.

## Navigation Model

A left navigation rail with tooltips. It maximises screen space and scales to future screens and providers.

## Global Application Layout

```text
Navigation Rail | Content Area | Status Bar
```

The Dashboard is the default landing screen.

## Primary Navigation

Dashboard, Queue, Conflicts, Activity, History, Providers and Settings are first-class destinations. This is the authoritative destination list; document 24 does not add a Sync Relationships screen.

## Advanced Operational Surfaces

Metrics, Logs, Traces and Diagnostics are available through Settings > Advanced.

## Screen Responsibilities

| Screen | Responsibility |
|--------|----------------|
| Dashboard | Trust and health |
| Queue | Operation visibility |
| Conflicts | Conflict resolution |
| Activity | Live execution |
| History | Completed activity |
| Providers | Provider management |
| Settings | Configuration |

## Prohibited Responsibilities

- Dashboard is not a file browser.
- Queue does not resolve conflicts.
- Providers do not expose reconciliation logic.
- Settings does not mutate sync state directly.

## Screens versus Panels

Major destinations are screens. Contextual details (Conflict Details, Provider Details, Operation Details) use panels, flyouts or dialogs.

## Deep Linking

Dashboard cards navigate directly to the appropriate screen. Example: clicking a conflict count opens the Conflicts screen.

## Status Bar

A permanent status bar shows provider health, queue activity, error state and last activity time.

## Window Lifecycle

Closing the main window minimises CloudSync to the system tray. Synchronisation continues running.

## Explicit Exit

CloudSync exits only through an explicit Exit CloudSync command from the tray menu.

## Tray Menu

Open Dashboard, Pause Sync, Resume Sync, Settings and Exit CloudSync.

## Future Extensibility

The rail accommodates future providers, observability screens, support tools and UI features without redesigning primary navigation.

## Architectural Alignment

If CloudSync is doing work, that work should be visible.

## Success Criteria

Users locate operational information quickly while retaining confidence that CloudSync keeps protecting and synchronising their data in the background.

## Illustrative Navigation Layout

```text
🏠 Dashboard
⏳ Queue
⚠ Conflicts
📈 Activity
📜 History
☁ Providers
⚙ Settings

Settings
  └─ Advanced
      ├─ Metrics
      ├─ Logs
      ├─ Traces
      └─ Diagnostics
```

# 17 Dashboard and User Trust Overview

## Purpose

Defines the CloudSync main dashboard. The dashboard is the primary user experience and answers four questions:

```text
Is my data safe?
Is synchronisation healthy?
Is progress being made?
Do I need to do anything?
```

## Design Principles

```text
Correctness
    ↓
Observability
    ↓
Recoverability
    ↓
User Trust
```

The UI exists to create confidence, not merely display activity.

## Trust-First Design

CloudSync is a trust system. The dashboard continually answers: can I trust CloudSync with my data right now?

## Information Hierarchy

1. Trust Status
2. Current Progress
3. Attention Required
4. Providers
5. Operation Queue
6. Current Activity
7. Recent Events

## Primary Dashboard Wireframe

```text
CloudSync                                                    Settings

✅ YOUR DATA IS SAFE
All selected sync scopes are synchronised.
No action required.

Last successful reconciliation : 14:32:11
Last provider poll             : 14:32:58
Last queue activity            : 14:33:02

SYNC PROGRESS
183,442 / 702,144 items analysed
Phase               : Building remote state
Throughput          : 4,232 items/sec
Estimated Remaining : 17 minutes

PROVIDERS
OneDrive - Healthy

QUEUE
Pending   : 1,245
Running   : 8
Completed : 181,223
Failed    : 0

ATTENTION REQUIRED
Conflicts : 1
Errors    : 0
Warnings  : 2

CURRENT ACTIVITY
Download: Documents/Architecture/Design.md
Upload: Photos/2026/Holiday/Image123.jpg
Rename: ProjectA -> ProjectArchive

RECENT EVENTS
Queue drained
Remote rename detected
Reconciliation completed
Sync scope updated
```

## Trust Status Card

The most important component in the application. Health states: Healthy, Active, Degraded, Attention Required and Critical.

## Formal Health Model

```text
SyncHealth = Healthy | Active | Degraded | AttentionRequired | Critical
```

## Progress Visibility

Never show only "Synchronising". Always show measurable progress, throughput and estimated remaining time whenever possible.

## Reconciliation Visibility

Expose pipeline stages:

- Enumerate Local State
- Enumerate Remote State
- Build Known State
- Analyse Changes
- Generate Operations
- Execute Queue

## Queue Visibility

The operation queue is the heart of the daemon and is always summarised on the dashboard.

## Attention Required

Always visible, even when all counts are zero.

## Dashboard Exclusions

- No file browser
- No sync scope configuration
- No detailed provider configuration
- No advanced diagnostics

## Architecture Alignment

If CloudSync is doing work, that work should be visible. The dashboard is a trust dashboard rather than a control panel.

## Success Criteria

Users can immediately determine whether their data is safe, whether synchronisation is healthy, whether progress is occurring and whether action is required.

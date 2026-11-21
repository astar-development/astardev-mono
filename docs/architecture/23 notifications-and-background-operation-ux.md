# 23 Notifications and Background Operation UX

## Purpose

Defines how CloudSync communicates important information while running in the background, particularly when the dashboard is not visible.

## Core Principle

Notifications build trust, not noise. Every notification helps the user understand what happened, what is happening or whether action is required.

## Trust Model

Users never have to wonder whether CloudSync is working, stalled or requires attention.

## Notification Categories

Informational, Progress, Warning, Conflict, Error and Critical.

## Background Operation Philosophy

CloudSync runs continuously from the system tray and communicates significant events without requiring the main window to remain open.

## Tray Integration

Closing the main window minimises CloudSync to the tray. Synchronisation continues until the user explicitly exits.

## Tray States

Healthy, Active, Attention Required and Critical are visible through tray icon appearance and tooltips.

## Progress Notifications

Long-running operations provide meaningful progress information rather than generic messages.

## Conflict Notifications

Conflicts always generate a notification with a direct route to the conflict resolution experience.

## Authentication Notifications

Provider authentication failures and expiring credentials are surfaced immediately.

## Error Notifications

Errors explain impact, consequence and available recovery actions.

## Notification Fatigue

Avoid repetitive notifications. Repeated events are aggregated where practical.

## Deep Linking

Notifications navigate directly to the relevant screen, provider, conflict or activity record.

## Quiet Operation

Routine successful synchronisation does not continuously interrupt users.

## Accessibility and Localisation

Notifications support localisation, accessibility technologies and all supported themes.

## Relationship to Dashboard

Notifications extend the trust dashboard when the dashboard is not visible.

## Success Criteria

Users understand whether CloudSync is healthy, active or requires attention without opening the application.

## Illustrative Background Flow

```text
Background Sync → Progress Update → Completion or Issue Detected → Notification → Deep Link → Resolution
```

## Key Principle

A notification answers one of three questions: what happened, what is happening, or what should I do next?

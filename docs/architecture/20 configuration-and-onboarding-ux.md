# 20 Configuration and Onboarding UX

## Purpose

Defines the first-run experience, onboarding workflow, configuration philosophy and ongoing configuration experience.

## UX Principles

- Trust before automation.
- Explicit rather than implicit configuration.
- Visibility of consequences.
- Predictability over convenience.

## First Run Experience

CloudSync never begins synchronising automatically. Users understand what will happen before data movement begins.

## Onboarding Flow

```text
Welcome
  ↓
Connect Provider
  ↓
Select Sync Scopes
  ↓
Review Configuration
  ↓
Start Initial Synchronisation
  ↓
Dashboard
```

## Welcome Screen

Introduces CloudSync as a trust-focused synchronisation application and sets expectations for onboarding.

## Provider Connection

The user connects a supported provider such as OneDrive. Authentication success, failure and reauthentication paths are clearly visible.

## Selective Sync Scope Selection

Users select folders, nested folders, multiple scopes and root files. Nothing synchronises until explicit scopes are selected.

## Configuration Review

Before synchronisation begins, CloudSync presents a summary: provider, local path, selected scopes, estimated items and available storage.

## Initial Synchronisation Experience

Progress is measurable and visible: current phase, item counts, throughput and estimated remaining time.

## Settings Architecture

Categories: General, Providers, Synchronisation, Appearance, Notifications and Advanced.

## Advanced Settings

Advanced contains Metrics, Logs, Traces and Diagnostics.

## Provider Management

Users add, remove, pause, resume and reauthenticate providers using explicit workflows.

## Sync Scope Management

Users add, remove or modify synchronisation scopes after onboarding.

## Storage Configuration

Users change the sync root location through a guided workflow with validation and safety checks.

## Configuration UX Rules

- Explain consequences.
- Avoid internal terminology.
- Never hide impactful behaviour.
- Always communicate what happens next.

## Relationship to Other Documents

Builds on documents 17, 18 and 19, extending the user-trust model into onboarding and configuration.

## Success Criteria

Users always understand what CloudSync will do, why, where data is stored, what is happening and how configuration can be adjusted later.

## Key Principle

Every configuration action makes the resulting synchronisation behaviour more predictable.

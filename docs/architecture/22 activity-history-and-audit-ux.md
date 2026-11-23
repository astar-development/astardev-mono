# 22 Activity, History and Audit UX

## Purpose

Defines how CloudSync presents current activity, historical events and audit information to support observability, recoverability and user trust.

## Core Principle

Users never need to guess what CloudSync has done. Significant actions are visible, discoverable and explainable.

## Activity, History and Audit

- Activity: what is happening now?
- History: what happened?
- Audit: can I prove it happened?

## Trust Goals

Users understand what work is being performed, what completed, what failed, and which actions were taken by users or the system.

## Activity Screen

A live operational view of downloads, uploads, renames, deletions, conflict creation and queue execution.

## History Screen

Records significant completed events and allows review of previous synchronisation actions.

## Audit Screen Philosophy

Audit prioritises accountability and recoverability rather than troubleshooting alone.

Audit is its own navigation destination, a peer of Activity and History (document 19). It is not a tab of History. It lists the recorded audit entries, newest first.

## Event Visibility

Every significant user-visible operation generates a meaningful event with timestamp and outcome.

## Filtering and Search

Activity and history support filtering by provider, operation type, status, date range and affected path.

## Operation Details

Users inspect detailed information for a recorded operation without leaving the current workflow.

## Conflict Auditability

Conflict creation, modification and resolution are permanently recorded in the audit trail.

## Provider Events

Authentication changes, provider reconnections, configuration changes and scope modifications appear in history.

## Retention Strategy

History and audit retention favour accountability and trust while remaining configurable.

## Export Capability

Users can export activity and audit information for support, troubleshooting and record keeping.

## Dashboard Integration

Recent Events on the Dashboard summarise the underlying activity and history systems.

## Observability Alignment

Logs, Metrics and Traces remain observability tools. Activity and History provide user-friendly visibility of important actions.

## Accessibility and Localisation

All activity and audit information is accessible, theme-aware and fully localisable.

## Success Criteria

Users determine what happened, when, why and whether recovery actions are available.

## Illustrative User Journey

```text
Dashboard Alert → Activity View → Operation Details → History Review → Audit Confirmation → User Confidence
```

## Key Principle

If CloudSync performs a significant action, users can find evidence of that action later.

# 21 Conflict Resolution UX

## Purpose

Defines how CloudSync presents, explains, manages and resolves synchronisation conflicts while preserving correctness, observability, recoverability and user trust.

This document defines presentation and flow. Behavioural rules and the resolution option set are owned by document 08; where this document lists different options, document 08 wins.

## Core Principle

Conflicts are preferable to silent data loss. When CloudSync cannot safely determine the correct outcome, it creates a conflict and involves the user.

## Conflict Philosophy

The goal is not to hide conflicts. The goal is to make conflicts understandable, recoverable and safe to resolve.

## User Trust Goals

Users understand what happened, why CloudSync detected a conflict, which versions exist and how to recover from an incorrect choice.

## Conflict Detection

Conflicts occur when local and remote changes cannot be reconciled safely through deterministic rules.

## Dashboard Integration

Conflicts appear in the Attention Required section of the dashboard and contribute to overall trust status.

## Conflict List Screen

A clear list of unresolved conflicts, affected items, conflict age and current status.

## Conflict Detail View

Local version details, remote version details, timestamps, sizes, paths and the reason the conflict was created.

## Side-by-Side Comparison

Where practical, local and remote metadata are shown side by side to support informed decisions.

## Resolution Options

Keep Local, Keep Remote, Rename Both, Create Copies, Manual Review and future provider-specific workflows. The authoritative MVP set (document 08, slice S21) is keep local, keep remote, keep both, defer and bulk actions.

## Recovery Guarantees

Resolving a conflict never destroys the alternative version immediately. Users retain a recovery path.

## Auditability

Conflict creation, review and resolution are recorded in activity history and observability systems.

## Bulk Resolution

Users may resolve multiple conflicts simultaneously when the chosen action is safe and clearly understood.

## Notifications

Conflict notifications explain the impact and provide a direct route to the conflict resolution screen.

## Accessibility and Localisation

Conflict status and meaning never depend solely on colour. All conflict text participates in localisation.

## Relationship to Architecture

Reinforces correctness, observability, recoverability and user trust.

## Success Criteria

Users understand what happened, why the conflict exists, how to resolve it and how to recover from a mistake.

## Illustrative Conflict Flow

```text
Conflict Detected → Explain Conflict → Present Options → User Decision → Confirmation → Recovery Available → Audit Recorded
```

## Key Principle

If CloudSync cannot guarantee the correctness of an automated decision, the user is asked rather than risking silent data loss.

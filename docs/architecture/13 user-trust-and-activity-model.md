# 13 User Trust and Activity Model

## Architectural Principles

### AStarDev.CloudSync Is A Trust System

AStarDev.CloudSync does not merely move files.

AStarDev.CloudSync maintains user trust that:

- Data is safe
- Synchronisation is healthy
- Progress is being made
- Problems are visible
- Recovery is possible

Correctness and trust take precedence over convenience.

---

### Every Significant Daemon State Must Have A User-Visible Representation

If the daemon knows something important:

```text
The user should be able to see it.
```

Examples:

```text
Indexing
Synchronising
Rate Limited
Conflict Detected
Paused
Recovering
Authentication Expired
```

---

### Evidence Of Progress Builds Trust

A user should never need to wonder:

```text
Is it stuck?
```

Long-running work must provide visible evidence of forward progress.

---

### Ambiguity Reduces Trust

Bad:

```text
Synchronising...
```

Good:

```text
Synchronising

183,442 / 702,144 items indexed

ETA: 17 minutes
```

---

## Core User Questions

For every significant workflow, the user should be able to answer:

```text
What is AStarDev.CloudSync doing?

Why is it doing it?

Is progress being made?

Does it need me?

Is my data safe?
```

If any answer is unavailable, additional activity reporting should be added.

---

## Trust States

### Healthy

User sees:

```text
Up To Date

Last Successful Sync:
08:14

No Conflicts
```

---

### Working

User sees:

```text
Synchronising

4,300 / 25,000 changes processed
```

---

### Waiting

User sees:

```text
Waiting For OneDrive

Retrying In 27 Seconds
```

Instead of appearing stuck.

---

### Action Required

User sees:

```text
2 Conflicts Require Review
```

or

```text
Account Reauthentication Required
```

---

### Recovering

User sees:

```text
Recovering Queue State

143 Operations Remaining
```

---

## Activity Model

Every activity should expose:

```text
Activity Name

Current State

Progress

Start Time

Last Update Time

User Action Required?
```

---

## Initial Crawl Example

Daemon Knows:

```text
183,442 / 702,144 indexed
```

User Sees:

```text
Initial Indexing

183,442 / 702,144 items indexed

26%

Estimated Remaining:
17 minutes
```

---

## Delta Processing Example

Daemon Knows:

```text
43,282 changes processed
```

User Sees:

```text
Processing Changes

43,282 / 91,500 completed
```

---

## Download Example

User Sees:

```text
Downloading

2.1 GB / 5.4 GB
```

---

## Conflict Example

Bad:

```text
Sync Failed
```

Good:

```text
2 Sync Conflicts Detected

No data has been discarded.

Review Required.
```

---

## Rate Limit Example

Bad:

```text
Synchronising...
```

Good:

```text
Waiting For OneDrive

Retrying In 29 Seconds
```

---

## Activity Reporting Principles

### The Daemon Is The Authority

The daemon owns activity state.

The UI displays activity state.

The UI does not infer activity state.

---

### Progress Is Reported, Not Calculated

Bad:

```text
UI guesses progress
```

Good:

```text
Daemon publishes progress
UI renders progress
```

---

### Progress Updates Are Throttled

Avoid:

```text
1
2
3
4
5
...
```

Prefer:

```text
500
1000
1500
2000
```

or time-based reporting.

---

## Notifications

Notify users when:

```text
Conflict Detected
Authentication Expired
Initial Crawl Complete
Sync Scope Complete
User Action Required
```

Avoid notifications for routine healthy activity.

---

## User Trust Dashboard

A user should always be able to see:

```text
Sync Status

Last Successful Sync

Pending Operations

Open Conflicts

Provider Health

Current Activity
```

without opening logs.

---

## Relationship To Observability

Observability serves:

```text
Developers
Operators
Diagnostics
```

Activity reporting serves:

```text
Users
```

The two systems should share the same source of truth.

---

## Architecture Review Checklist

```text
□ Every long-running activity has a user-visible state

□ Every user-visible state originates from daemon activity

□ No significant workflow appears idle when active

□ Progress reporting is available where meaningful

□ User action requirements are explicit

□ Conflicts clearly state that data is preserved

□ Recovery operations expose status

□ Activity updates are throttled appropriately
```

---

## Architectural Summary

The goal is not merely to perform synchronisation.

The goal is to ensure users understand that synchronisation is occurring.

AStarDev.CloudSync should never reach a state where:

```text
The daemon knows progress is being made

but

The user believes the system is stuck.
```

Trust is built through visibility.

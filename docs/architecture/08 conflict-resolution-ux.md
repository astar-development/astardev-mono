# 8 Conflict Resolution UX

## Architectural Principles

### User Data Is More Important Than Convenience

AStarDev.CloudSync prioritises data safety over automation.

When uncertainty exists:

```text
Create Conflict
```

rather than:

```text
Guess
```

---

### A Conflict Is Not An Error

Conflicts are expected outcomes.

They indicate:

```text
AStarDev.CloudSync detected competing user intent
and refused to lose data.
```

Conflicts should be presented calmly and clearly.

---

### Conflicts Must Be Actionable

The user must always understand:

- What happened
- Why it happened
- What options are available
- Which option preserves the most data

---

## UX Goals

The user should never ask:

```text
Why did this happen?
```

or

```text
What will happen if I click this?
```

The interface should make both obvious.

---

## Conflict Centre

Conflicts are managed through a dedicated area:

```text
Conflict Centre
```

Rather than intrusive modal dialogs.

Example:

```text
AStarDev.CloudSync

Sync Jobs: 3
Conflicts: 2

[ Review Conflicts ]
```

---

## Conflict Lifecycle

```text
Conflict Detected
        ↓
Persist Conflict
        ↓
Publish Event
        ↓
Display To User
        ↓
User Decision
        ↓
Resolve Conflict
        ↓
Resume Synchronisation
```

---

## Conflict Categories

### Concurrent Modification

Example:

```text
Known   = A
Local   = B
Remote  = C
```

User message:

```text
This file was changed locally and in OneDrive.
AStarDev.CloudSync cannot safely determine which version should win.
```

---

### Rename Conflict

Example:

```text
Local
    Reports/Q1.xlsx

Remote
    Archive/Q1.xlsx
```

User message:

```text
This item was moved to different locations.
```

---

### Delete Conflict

Example:

```text
Local
    Modified

Remote
    Deleted
```

User message:

```text
One side changed this file while the other removed it.
```

---

## Default Information Display

Every conflict should display:

- File name
- Relative path
- Conflict type
- Local timestamp
- Remote timestamp
- Local size
- Remote size
- Detection time

Example:

```text
Report.xlsx

Conflict Type
    Concurrent Modification

Local
    Modified 08:14

Remote
    Modified 08:17
```

---

## Resolution Actions

### Keep Local Version

Result:

```text
Upload Local Version
Update Known State
Resolve Conflict
```

---

### Keep Remote Version

Result:

```text
Download Remote Version
Update Known State
Resolve Conflict
```

---

### Keep Both Versions

Preferred safe option.

Result:

```text
Original File
Conflict Copy
```

Example:

```text
Report.xlsx
Report (Conflict - 2026-11-19 081417).xlsx
```

---

### Defer Decision

Result:

```text
Conflict Remains Open
```

Synchronisation of unrelated items continues.

---

## Bulk Resolution

Users may resolve multiple conflicts.

Examples:

```text
Keep Local For Selected
Keep Remote For Selected
Keep Both For Selected
```

Bulk actions should never be automatic.

---

## Notifications

Conflicts should be visible but not disruptive.

Preferred behaviour:

```text
Tray Badge
Notification
Conflict Counter
```

Avoid:

```text
Blocking Dialogs
```

except when initiated by the user.

---

## Auditability

Every conflict resolution must be recorded.

Example:

```text
ConflictDetected
ConflictResolved
ResolutionType
User Decision
Timestamp
```

Stored within the Event Journal.

---

## Safety Rules

### Rule 1

AStarDev.CloudSync never destroys data automatically during conflict resolution.

---

### Rule 2

A user decision must always be reversible where practical.

---

### Rule 3

Keep Both is preferred over destructive behaviour.

---

### Rule 4

The safest option should be visually promoted.

---

## Success Criteria

The ideal user response is:

```text
I understand why AStarDev.CloudSync stopped.
I understand my options.
I trust AStarDev.CloudSync not to lose my data.
```

Preserving trust is more important than minimising the number of conflicts.

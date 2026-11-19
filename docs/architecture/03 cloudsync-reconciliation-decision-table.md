# Reconciliation Decision Table

## Overview

Rather than modelling every possible permutation directly, reconciliation is performed in two phases:

1. Determine the change set for Local relative to Known.
2. Determine the change set for Remote relative to Known.
3. Resolve the resulting pair of change sets.

---

## Phase 1 - Change Detection

```csharp
[Flags]
public enum ChangeSet
{
    None     = 0,

    Created  = 1 << 0,
    Deleted  = 1 << 1,
    Renamed  = 1 << 2,
    Modified = 1 << 3
}
```

### Examples

| Known | Current | ChangeSet |
| ------- | ------- | ------- |
| Missing | Exists | Created |
| Exists | Missing | Deleted |
| Path changed | Exists | Renamed |
| Content changed | Exists | Modified |

---

## Phase 2 - Decision Resolution

### Legend

```text
L = Local ChangeSet
R = Remote ChangeSet
```

---

## No Changes

| L | R | Decision |
| - | - | - |
| None | None | NoOp |

---

## Creates

| L | R | Decision |
| - | - | - |
| Created | None | CreateRemote |
| None | Created | CreateLocal |

---

## Modifications

| L | R | Decision |
| - | - | - |
| Modified | None | UpdateRemote |
| None | Modified | UpdateLocal |

---

## Renames

| L | R | Decision |
| - | - | - |
| Renamed | None | RenameRemote |
| None | Renamed | RenameLocal |

---

## Deletes

| L | R | Decision |
| - | - | - |
| Deleted | None | DeleteRemote |
| None | Deleted | DeleteLocal |

---

## Symmetric Deletes

| L | R | Decision |
| - | - | - |
| Deleted | Deleted | NoOp |

Both sides independently agree the item should no longer exist.

---

## Concurrent Content Changes

| L | R | Decision |
| - | - | - |
| Modified | Modified | Conflict |

### Principle

```text
Content vs Content
    =
Conflict
```

---

## Concurrent Renames

### Same Rename

```text
Known
    /Foo

Local
    /Bar

Remote
    /Bar
```

Decision:

```text
NoOp
```

---

### Different Renames

```text
Known
    /Foo

Local
    /Bar

Remote
    /Baz
```

Decision:

```text
Conflict
```

---

## Rename + Content Change

| L | R | Decision |
| - | - | - |
| Renamed | Modified | Merge |
| Modified | Renamed | Merge |

### Principle

Location and content are orthogonal concerns.

```text
Rename
+
Content Update
```

can be safely merged.

---

## Rename + Delete

### Example

```text
Known
    Report.docx

Local
    Report-2026.docx

Remote
    Missing
```

Decision:

```text
Conflict
```

### Principle

```text
Keep
vs
Delete
```

is a competing intent.

---

## Modify + Delete

### Example

```text
Known
    Report.docx

Local
    Modified

Remote
    Deleted
```

Decision:

```text
Conflict
```

### Principle

```text
Keep
vs
Delete
```

is a competing intent.

---

## Create + Create

### Same Identity

Decision:

```text
NoOp
```

### Different Content

Decision:

```text
Conflict
```

The engine must never guess which version should survive.

---

# Master Rules

## Rule 1

If neither side changed:

```text
NoOp
```

---

## Rule 2

If exactly one side changed:

```text
Apply that change
```

---

## Rule 3

If both sides changed different concerns:

```text
Merge
```

Examples:

```text
Rename + Modify
```

---

## Rule 4

If both sides changed the same concern differently:

```text
Conflict
```

Examples:

```text
Modify + Modify
Rename + Rename
```

---

## Rule 5

If one side expresses:

```text
Keep
```

and the other expresses:

```text
Delete
```

then:

```text
Conflict
```

Examples:

```text
Modify + Delete
Rename + Delete
```

---

## Architectural Summary

The reconciler is not attempting to find the most likely outcome.

It is attempting to find the safest correct outcome.

When uncertainty exists:

```text
Conflict
```

is preferred over:

```text
Guessing
```

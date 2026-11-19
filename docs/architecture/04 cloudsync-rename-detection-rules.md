# Rename Detection Rules

## Design Principles

### Identity Is Stronger Than Path

A path change does not imply a new item.

```text
/foo/report.docx

→

/archive/report.docx
```

is presumed to be a rename until proven otherwise.

---

### Rename Detection Is Deterministic

AStarDev.CloudSync must not guess.

Rename detection should be based on known identities and observations.

```text
Confidence
    >= Threshold

        → Rename

Otherwise

        → Delete + Create
```

---

### Provider Identity Always Wins

For remote items:

```text
RemoteItemId
```

is the authoritative identity.

Example:

```text
Known
    ItemId = 123
    Path   = /foo/report.docx

Remote
    ItemId = 123
    Path   = /bar/report.docx
```

Decision:

```text
Rename
```

Even if every path segment changed.

---

## Detection Sources

Rename detection uses three independent signals.

### Signal 1 - Stable Identity

Highest confidence.

Examples:

```text
OneDrive ItemId
Filesystem Identity
```

Detection:

```text
Identity unchanged
Path changed
```

Result:

```text
Rename
```

---

### Signal 2 - Fingerprint Match

Medium confidence.

Examples:

```text
Size
Hash
ModifiedUtc
```

Detection:

```text
Deleted Item
+
Created Item
with matching fingerprint
```

Result:

```text
Likely Rename
```

---

### Signal 3 - Temporal Correlation

Lowest confidence.

Examples:

```text
Delete observed

immediately followed by

Create observed
```

This signal alone is never sufficient.

---

## Local Rename Detection

### Preferred Rule

Use filesystem identity when available.

```text
Identity = ABC

Known
    /foo/report.docx

Current
    /archive/report.docx
```

Decision:

```text
Rename
```

---

### Secondary Rule

If filesystem identity is unavailable:

```text
Delete
+
Create
```

may be correlated using:

```text
Size
Fingerprint
ModifiedUtc
```

---

## Remote Rename Detection

### Rule

Remote rename detection is based entirely on:

```text
RemoteItemId
```

Example:

```text
Known

ItemId = 777
Path   = /foo/report.docx

Remote

ItemId = 777
Path   = /bar/report.docx
```

Decision:

```text
Rename
```

No further evidence required.

---

## Rename Classification

AStarDev.CloudSync treats moves and renames as the same operation.

```text
report.docx
→
report-final.docx
```

and

```text
/reports/report.docx
→
/archive/report.docx
```

Both become:

```text
Rename
```

because:

```text
Path changed
Identity unchanged
```

---

## Rename Decision Matrix

### Local Rename

```text
Known
    /foo/report.docx

Local
    /archive/report.docx

Remote
    /foo/report.docx
```

Decision:

```text
RenameRemote
```

---

### Remote Rename

```text
Known
    /foo/report.docx

Local
    /foo/report.docx

Remote
    /archive/report.docx
```

Decision:

```text
RenameLocal
```

---

### Same Rename

```text
Known
    /foo/report.docx

Local
    /archive/report.docx

Remote
    /archive/report.docx
```

Decision:

```text
NoOp
```

---

### Different Rename

```text
Known
    /foo/report.docx

Local
    /archive/report.docx

Remote
    /work/report.docx
```

Decision:

```text
Conflict
```

---

## Rename + Content Change

### Local Rename, Remote Modify

```text
Known
    report.docx

Local
    archive/report.docx

Remote
    report.docx (modified)
```

Decision:

```text
Merge
```

Result:

```text
Rename
+
Content Update
```

---

### Remote Rename, Local Modify

Decision:

```text
Merge
```

---

## Rename + Delete

### Local Rename, Remote Delete

Decision:

```text
Conflict
```

Reason:

```text
Keep
vs
Delete
```

---

### Remote Rename, Local Delete

Decision:

```text
Conflict
```

---

## Ambiguous Detection

When AStarDev.CloudSync cannot prove a rename:

```text
Delete
+
Create
```

must remain:

```text
Delete
+
Create
```

The engine must never invent a rename.

---

## Architectural Rule

The engine should prefer:

```text
False Negative Rename
```

over:

```text
False Positive Rename
```

A false positive rename can corrupt state tracking.
A false negative rename typically results in a less efficient sync operation.

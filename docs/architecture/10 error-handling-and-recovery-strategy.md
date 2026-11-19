# 10 Error Handling and Recovery Strategy

## Architectural Principles

### Errors Are Expected

AStarDev.CloudSync treats failures as a normal operating condition.

Examples:

- Network interruptions
- Provider throttling
- File locks
- Permission issues
- Disk space exhaustion
- Process crashes
- Invalid delta tokens

The system must continue operating whenever possible.

---

### Recovery Before Failure

Every component should be designed around:

```text
Detect
    ↓
Recover
    ↓
Continue
```

rather than:

```text
Detect
    ↓
Stop
```

---

### One Item Must Not Stop Synchronisation

A failure affecting one item must not prevent:

```text
Remaining Operations
Remaining Scopes
Remaining Sync Jobs
```

from continuing.

---

### Failure Is A Value

Any method that can fail returns a `Result<TResult, TError>` (or `Option<T>`
when a value may be absent) from `AStarDev.FunctionalParadigm`.

Expected failures are not signalled with exceptions or `null`. Code that calls
throwing APIs wraps the call (`Try.Run` / `Try.RunAsync`) and maps the failure
to an error value; there are no `try`/`catch` blocks in application code.

Exceptions remain only for programmer errors and framework contracts, for
example a hosted service that must stop the host from starting.

---

### Cancellation Is Not A Failure

Every async method takes a `CancellationToken` as its last parameter, passes
it on, and honours it. Cancellation propagates as
`OperationCanceledException`; it is never converted into an error value or
retried.

---

### User Trust Is More Important Than Hiding Errors

AStarDev.CloudSync should:

```text
Explain Errors
```

not:

```text
Suppress Errors
```

---

## Error Classification

### Transient Errors

Failures likely to succeed when retried.

Examples:

```text
Network Failure
Provider Timeout
Temporary File Lock
Rate Limiting
```

Action:

```text
Retry
```

---

### Permanent Errors

Failures unlikely to succeed without intervention.

Examples:

```text
Access Denied
Missing Permissions
Invalid Configuration
Unsupported Path
```

Action:

```text
Dead Letter
Notify User
```

---

### Conflict Errors

Intent ambiguity.

Examples:

```text
Modify + Modify
Rename + Rename
Delete + Modify
```

Action:

```text
Create Conflict
```

---

### Corruption Errors

Unexpected state inconsistencies.

Examples:

```text
Invalid Database State
Broken Snapshot Chain
Identity Mismatch
```

Action:

```text
Quarantine
Diagnostic Logging
Recovery Workflow
```

---

## Error Translation

Provider-specific exceptions must not cross architectural boundaries.

Provider:

```text
GraphException
HttpException
```

becomes the following `ProviderError` values:

```csharp
RateLimited      // carries RetryAfter
Transient
AuthenticationFailed
Permanent
```

These are a closed set of error values, `ProviderError`, declared in the
Domain. A provider returns them inside a `Result`; it does not throw them.

Cancellation is not a failure. If the caller cancels, the provider honours the
`CancellationToken` and lets `OperationCanceledException` propagate. It is
never converted into a `ProviderError`.

---

## Retry Strategy

### Exponential Backoff

Recommended pattern:

```text
Attempt 1
Attempt 2
Attempt 3
Attempt 4
```

with increasing delay between attempts.

---

### Maximum Retry Count

Every operation has a bounded retry count.

After the threshold is exceeded:

```text
DeadLetter
```

---

## Dead Letter Queue

Dead-lettered operations remain visible.

Users may:

```text
Retry
Ignore
Investigate
```

No automatic deletion.

---

## Daemon Recovery

### Startup Recovery

On startup:

```text
Load Queue
    ↓
Locate Running Operations
    ↓
Mark Failed
    ↓
Requeue Eligible Operations
```

---

### Database Recovery

If database integrity checks fail:

```text
Enter Recovery Mode
```

Normal synchronisation stops.

Diagnostics are preserved.

---

## Provider Recovery

### Authentication Expired

Action:

```text
Pause Provider
Notify User
Reconnect
Resume
```

---

### Delta Token Invalid

Action:

```text
Invalidate Token
Scoped Reindex
Acquire New Token
Resume Delta Mode
```

---

### Rate Limiting

Action:

```text
Pause Requests
Backoff
Resume
```

The provider module owns provider-specific throttling details.

---

## Filesystem Recovery

### Locked File

Action:

```text
Retry Later
```

---

### Access Denied

Action:

```text
Dead Letter
Notify User
```

---

### Missing Path

Action:

```text
Reconcile Current State
```

Never assume corruption.

---

## Observability Requirements

Every error should record:

- Timestamp
- Operation Id
- Item Id
- Sync Job Id
- Error Classification
- Retry Count
- Root Exception

---

## User Experience

Users should see:

```text
What failed
Why it failed
What AStarDev.CloudSync is doing
Whether action is required
```

Avoid:

```text
Unhandled Exceptions
Raw Stack Traces
```

in normal workflows.

---

## Architectural Summary

AStarDev.CloudSync should be designed to survive:

- Process crashes
- Provider failures
- Network loss
- Invalid tokens
- Locked files
- User mistakes

The objective is not to prevent every failure.

The objective is:

```text
Detect
Recover
Continue
Preserve User Data
```

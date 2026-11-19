# 9 Provider Abstraction and OneDrive Integration

## Architectural Principles

### The Sync Engine Owns Synchronisation

The Sync Engine is responsible for:

- Reconciliation
- Conflict Detection
- Rename Detection
- Operation Planning
- State Management

Providers do not participate in these decisions.

Provider responsibilities are limited to:

```text
Observe Remote State
Execute Remote Operations
```

---

### Providers Expose Capabilities

Providers are abstracted by behaviour.

Good:

```text
List Changes
Download File
Upload File
Delete Item
```

Bad:

```text
Graph Delta
Drive Item
Change Token
```

Provider-specific terminology must not leak into the Sync Engine.

---

### OneDrive Is The First Provider

The architecture supports multiple providers.

However:

```text
OneDrive First
```

is an intentional design decision.

---

## Architecture

```text
AStarDev.CloudSync.Domain
        ↑
        │
AStarDev.CloudSync.Ports
  (ICloudProvider)
        ↑
        │
AStarDev.CloudSync.OneDrive
        │
 Microsoft Graph
```

`ICloudProvider`, `ProviderError` and the provider models are declared in
`AStarDev.CloudSync.Ports`, not in the Domain. The Domain holds identities,
value objects and rules; the port is an I/O boundary, and I/O can fail. The
Sync Engine and OneDrive both reference Ports. Neither references the other.

The Sync Engine communicates exclusively through:

```csharp
ICloudProvider
```

---

## Failure Is A Value, Cancellation Is Honoured

Every provider method that can fail returns a
`Result<TResult, ProviderError>` from `AStarDev.FunctionalParadigm`. Expected
failures are never signalled with exceptions or `null`.

Every async method takes a `CancellationToken` as its last parameter, passes it
to every call that accepts one, and honours cancellation.

Provider code that calls throwing APIs (Graph SDK, HTTP) wraps the call so the
failure becomes a `ProviderError`; it does not use `try`/`catch` blocks.

---

## Provider Responsibilities

Providers own:

- Authentication
- Remote Enumeration
- Delta Discovery
- Content Upload
- Content Download
- Remote Rename
- Remote Delete
- Remote Metadata Retrieval

Providers do not own:

- Conflict Resolution
- Operation Planning
- Sync Decisions
- Database Updates
- Snapshot Management

---

## Core Provider Contract

```csharp
public interface ICloudProvider
{
    Task<Result<ProviderAccount, ProviderError>> ConnectAsync(
        CancellationToken cancellationToken);

    Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(
        SyncScope scope,
        CancellationToken cancellationToken);

    Task<Result<ProviderDeltaPage, ProviderError>> GetDeltasAsync(
        DeltaToken token,
        CancellationToken cancellationToken);

    Task<Result<Stream, ProviderError>> DownloadAsync(
        RemoteItemId itemId,
        CancellationToken cancellationToken);

    Task<Result<Unit, ProviderError>> UploadAsync(
        UploadRequest request,
        CancellationToken cancellationToken);

    Task<Result<Unit, ProviderError>> RenameAsync(
        RemoteItemId itemId,
        RelativePath path,
        CancellationToken cancellationToken);

    Task<Result<Unit, ProviderError>> DeleteAsync(
        RemoteItemId itemId,
        CancellationToken cancellationToken);
}
```

---

## Provider Models

Provider implementations return domain-friendly models, declared in
`AStarDev.CloudSync.Ports`. Identities such as `RemoteItemId` and
`RelativePath` stay in the Domain.

Forbidden across the boundary:

```text
DriveItem
GraphServiceClient
Graph SDK Models
```

Allowed:

```text
ProviderItem
ProviderDelta
ProviderAccount
```

---

## Provider Item

```csharp
public sealed record ProviderItem(
    RemoteItemId RemoteItemId,
    RelativePath Path,
    ItemType ItemType,
    long Size,
    DateTimeOffset ModifiedUtc);
```

---

## Provider Delta

```csharp
public sealed record ProviderDelta(
    RemoteItemId RemoteItemId,
    ProviderChangeType ChangeType,
    RelativePath Path);
```

Examples:

```text
Created
Modified
Deleted
Renamed
```

---

## Remote Identity

### RemoteItemId Is A Domain Concept

A provider must expose a stable remote identity.

```csharp
public readonly record struct RemoteItemId(string Value);
```

Examples:

```text
OneDrive Item Id
Google Drive File Id
Dropbox File Id
```

The implementation differs, but the business concept remains the same.

---

## Delta Synchronisation

The Sync Engine owns:

- Delta token storage
- Provider state
- Recovery strategy

The provider owns:

- Executing delta requests

---

## Error Translation

Provider-specific exceptions must never cross the provider boundary.

Translate:

```text
GraphException
HttpException
```

Into the following `ProviderError` values:

```csharp
RateLimited      // carries RetryAfter
Transient
AuthenticationFailed
Permanent
```

These are a closed set of error values, `ProviderError`, declared in Ports. A provider returns them inside a `Result`; it does not throw them.

Cancellation is not a failure. If the caller cancels, the provider honours the
`CancellationToken` and lets `OperationCanceledException` propagate. It is
never converted into a `ProviderError`.

---

## OneDrive Provider Responsibilities

- OAuth authentication
- Graph integration
- Delta queries
- Content transfer
- Retry handling
- Rate limit handling

Hidden implementation details:

```text
DriveItem
ETag
CTag
DeltaLink
NextLink
Graph Exceptions
```

---

## Testing Strategy

The Sync Engine must be testable without OneDrive.

Example:

```csharp
FakeCloudProvider
```

Capabilities:

- Return deltas
- Return files
- Simulate failures
- Simulate conflicts

---

## Future Providers

Examples:

```text
Google Drive
Dropbox
SharePoint
```

Adding a provider should primarily involve:

```text
Authentication
Provider Mapping
Remote Operations
```

The Sync Engine should require no architectural changes.

---

## Architectural Summary

The Sync Engine owns:

```text
Intent
State
Decisions
```

Providers own:

```text
Remote Access
Remote Execution
Provider Translation
```

The Sync Engine should be capable of running entirely against a fake provider.

If this remains true, the provider boundary is correctly defined.

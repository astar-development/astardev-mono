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
 ICloudProvider
        │
AStarDev.CloudSync.OneDrive
        │
 Microsoft Graph
```

The Sync Engine communicates exclusively through:

```csharp
ICloudProvider
```

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
    Task<ProviderAccount> ConnectAsync();

    Task<ProviderSnapshotPage> GetSnapshotAsync(
        SyncScope scope,
        CancellationToken cancellationToken);

    Task<ProviderDeltaPage> GetDeltasAsync(
        DeltaToken token,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAsync(
        RemoteItemId itemId,
        CancellationToken cancellationToken);

    Task UploadAsync(
        UploadRequest request,
        CancellationToken cancellationToken);

    Task RenameAsync(
        RemoteItemId itemId,
        RelativePath path,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        RemoteItemId itemId,
        CancellationToken cancellationToken);
}
```

---

## Provider Models

Provider implementations return domain-friendly models.

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

## Exception Translation

Provider-specific exceptions must never cross the provider boundary.

Translate:

```text
GraphException
HttpException
```

Into:

```csharp
RateLimitException
TransientProviderException
AuthenticationFailureException
PermanentProviderException
```

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

using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The disk could not be listed, read or watched, for example because the root is missing or unreadable.</summary>
/// <param name="Message">The underlying reason, suitable for logs.</param>
internal sealed record FileSystemFailed(string Message);

/// <summary>The observations could not be read from or written to the store.</summary>
/// <param name="Error">The store's reason.</param>
internal sealed record ObservationStoreFailed(StoreError Error);

/// <summary>Closed set of reasons local observation stopped or a scan did not complete.</summary>
internal union LocalObservationError(FileSystemFailed, ObservationStoreFailed);

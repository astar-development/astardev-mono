using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Reads the filesystem's own identity of a file. A port so the identity can be faked and so platforms without one still work.</summary>
internal interface ILocalIdentityReader
{
    /// <summary>Reads the identity of a file.</summary>
    /// <param name="fullPath">Absolute path of the file.</param>
    /// <returns>The identity, or null when the platform has none, the file vanished or it cannot be read. Never throws; null means rename detection falls back to the content fingerprint.</returns>
    LocalIdentity? Read(string fullPath);
}

namespace AStarDev.CloudSync.Persistence;

/// <summary>The mutable side of <see cref="IDatabaseState"/>, written only by the initialiser.</summary>
internal sealed class DatabaseState : IDatabaseState
{
    private sealed record Snapshot(DatabaseMode Mode, string? RecoveryReason);

    private Snapshot current = new(DatabaseMode.Uninitialised, null);

    /// <inheritdoc/>
    public DatabaseMode Mode => Volatile.Read(ref current).Mode;

    /// <inheritdoc/>
    public string? RecoveryReason => Volatile.Read(ref current).RecoveryReason;

    /// <summary>Records that the integrity check passed and migrations are applied. Does not leave recovery mode once entered.</summary>
    public void MarkNormal()
    {
        if (Mode != DatabaseMode.Recovery) Volatile.Write(ref current, new Snapshot(DatabaseMode.Normal, null));
    }

    /// <summary>Records that the database cannot be trusted. Stays in effect until the process restarts.</summary>
    /// <param name="reason">What the integrity check found.</param>
    public void EnterRecovery(string reason) => Volatile.Write(ref current, new Snapshot(DatabaseMode.Recovery, reason));
}

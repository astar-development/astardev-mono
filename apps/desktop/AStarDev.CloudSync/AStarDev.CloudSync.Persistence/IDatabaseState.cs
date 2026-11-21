namespace AStarDev.CloudSync.Persistence;

/// <summary>What startup learned about the database. Safe to read from any thread.</summary>
public interface IDatabaseState
{
    /// <summary>The current mode. Stays <see cref="DatabaseMode.Recovery"/> until the process restarts.</summary>
    DatabaseMode Mode { get; }

    /// <summary>Why recovery mode was entered, suitable for logs and the user. <see langword="null"/> unless <see cref="Mode"/> is <see cref="DatabaseMode.Recovery"/>.</summary>
    string? RecoveryReason { get; }
}

namespace AStarDev.CloudSync.Persistence;

/// <summary>Whether the database can be trusted for normal synchronisation.</summary>
public enum DatabaseMode
{
    /// <summary>Startup has not finished checking the database. Nothing may read or write sync state yet.</summary>
    Uninitialised,

    /// <summary>The integrity check passed and every migration is applied.</summary>
    Normal,

    /// <summary>The integrity check failed. Normal synchronisation must not run; the file is left untouched so it can be diagnosed.</summary>
    Recovery
}

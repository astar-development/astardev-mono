namespace AStarDev.CloudSync.Persistence;

/// <summary>Project-specific strings for persistence, so none are repeated as literals.</summary>
internal static class ApplicationConstants
{
    /// <summary>Name shared by the activity source and the meter, so the <c>AStarDev.CloudSync.*</c> wildcard collects both.</summary>
    public static string TelemetryName => "AStarDev.CloudSync.Persistence";

    /// <summary>The XDG variable that locates the user's data folder.</summary>
    public static string XdgDataHomeEnvironmentVariable => "XDG_DATA_HOME";

    /// <summary>Folder under the data home that holds the database file.</summary>
    public static string DatabaseDirectoryName => "astardev-cloudsync";

    /// <summary>Name of the SQLite database file.</summary>
    public static string DatabaseFileName => "cloudsync.db";

    /// <summary>Name of the telemetry SQLite database file.</summary>
    public static string TelemetryDatabaseFileName => "telemetry.db";

    /// <summary>Name the database health check is reported under.</summary>
    public static string DatabaseHealthCheckName => "database";

    /// <summary>Value of the <c>db.system.name</c> semantic convention attribute.</summary>
    public static string SqliteSystemName => "sqlite";
}

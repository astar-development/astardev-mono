namespace AStarDev.CloudSync.Persistence;

/// <summary>The <c>Telemetry:Database</c> configuration section: where logs, spans and metric samples are kept, apart from the sync database.</summary>
public sealed class TelemetryDatabaseOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string ConfigurationSection = "Telemetry:Database";

    /// <summary>
    /// The telemetry SQLite file. Defaults to <c>telemetry.db</c> in the folder of the default sync database, so it is never the same file as <see cref="DatabaseOptions.Path"/>.
    /// Setting <c>Database:Path</c> alone does not move it: set <c>Telemetry:Database:Path</c> too. The file is created by the first write.
    /// </summary>
    public string Path { get; set; } = DefaultPath(
        Environment.GetEnvironmentVariable(ApplicationConstants.XdgDataHomeEnvironmentVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Builds <c>&lt;data home&gt;/astardev-cloudsync/telemetry.db</c>.</summary>
    /// <param name="xdgDataHome">The <c>XDG_DATA_HOME</c> value. Null, empty or white space means unset.</param>
    /// <param name="homeDirectory">The user's home folder, used with <c>.local/share</c> when <paramref name="xdgDataHome"/> is unset.</param>
    /// <returns>The path of the telemetry file; nothing is created on disk.</returns>
    public static string DefaultPath(string? xdgDataHome, string homeDirectory)
        => System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(DatabaseOptions.DefaultPath(xdgDataHome, homeDirectory))!,
            ApplicationConstants.TelemetryDatabaseFileName);
}

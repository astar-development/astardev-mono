namespace AStarDev.CloudSync.Persistence;

/// <summary>The <c>Database</c> configuration section: where the daemon keeps its SQLite file.</summary>
public sealed class DatabaseOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string ConfigurationSection = "Database";

    /// <summary>
    /// The SQLite database file. Set <c>Database:Path</c> to override the <see cref="DefaultPath"/> for the current <c>XDG_DATA_HOME</c>.
    /// The folder is created at startup when it is missing; the file is created by the first migration.
    /// </summary>
    public string Path { get; set; } = DefaultPath(
        Environment.GetEnvironmentVariable(ApplicationConstants.XdgDataHomeEnvironmentVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Builds <c>&lt;data home&gt;/astardev-cloudsync/cloudsync.db</c>.</summary>
    /// <param name="xdgDataHome">The <c>XDG_DATA_HOME</c> value. Null, empty or white space means unset.</param>
    /// <param name="homeDirectory">The user's home folder, used with <c>.local/share</c> when <paramref name="xdgDataHome"/> is unset.</param>
    /// <returns>The path of the database file; nothing is created on disk.</returns>
    public static string DefaultPath(string? xdgDataHome, string homeDirectory)
        => System.IO.Path.Combine(
            string.IsNullOrWhiteSpace(xdgDataHome) ? System.IO.Path.Combine(homeDirectory, ".local", "share") : xdgDataHome,
            ApplicationConstants.DatabaseDirectoryName,
            ApplicationConstants.DatabaseFileName);
}

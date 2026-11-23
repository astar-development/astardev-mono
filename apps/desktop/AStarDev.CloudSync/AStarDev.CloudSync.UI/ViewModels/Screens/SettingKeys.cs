namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The setting names the daemon accepts in <c>SetSetting</c>. The UI may not reference Ports, so the names are repeated here from <c>SettingNames</c>.</summary>
internal static class SettingKeys
{
    /// <summary>The UI theme name.</summary>
    public const string Theme = "theme";

    /// <summary>Whether a new conflict notifies, as <c>true</c> or <c>false</c>.</summary>
    public const string NotifyOnConflict = "notifications.conflicts";

    /// <summary>Whether a failed operation notifies, as <c>true</c> or <c>false</c>.</summary>
    public const string NotifyOnError = "notifications.errors";

    /// <summary>Whether a finished sync notifies, as <c>true</c> or <c>false</c>.</summary>
    public const string NotifyOnCompletion = "notifications.completion";

    /// <summary>Days audit entries are kept, a whole number from 1.</summary>
    public const string AuditRetentionDays = "retention.audit-days";

    /// <summary>Days telemetry is kept, a whole number from 1.</summary>
    public const string TelemetryRetentionDays = "retention.telemetry-days";
}

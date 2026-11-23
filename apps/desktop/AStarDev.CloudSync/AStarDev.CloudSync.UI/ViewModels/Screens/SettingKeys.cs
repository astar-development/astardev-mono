namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The setting names the daemon accepts in <c>SetSetting</c>. The UI may not reference Ports, so the names are repeated here from <c>SettingNames</c>.</summary>
internal static class SettingKeys
{
    public const string Theme = "theme";
    public const string NotifyOnConflict = "notifications.conflicts";
    public const string NotifyOnError = "notifications.errors";
    public const string NotifyOnCompletion = "notifications.completion";
    public const string AuditRetentionDays = "retention.audit-days";
    public const string TelemetryRetentionDays = "retention.telemetry-days";
}

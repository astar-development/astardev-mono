namespace AStarDev.CloudSync.Ports;

/// <summary>The names of the settings the daemon keeps. <see cref="ISettingsStore"/> accepts only these.</summary>
public static class SettingNames
{
    /// <summary>The UI theme: <c>Dark</c>, <c>Light</c> or <c>System</c>. Defaults to <c>Dark</c>.</summary>
    public const string Theme = "theme";

    /// <summary>Whether a new conflict raises a notification: <c>true</c> or <c>false</c>. Defaults to <c>true</c>.</summary>
    public const string NotifyOnConflict = "notifications.conflicts";

    /// <summary>Whether a failed operation raises a notification: <c>true</c> or <c>false</c>. Defaults to <c>true</c>.</summary>
    public const string NotifyOnError = "notifications.errors";

    /// <summary>Whether a finished sync raises a notification: <c>true</c> or <c>false</c>. Defaults to <c>false</c>.</summary>
    public const string NotifyOnCompletion = "notifications.completion";

    /// <summary>How many days audit entries are kept, a whole number from 1. Defaults to <c>90</c>.</summary>
    public const string AuditRetentionDays = "retention.audit-days";

    /// <summary>How many days telemetry is kept, a whole number from 1. Defaults to <c>7</c>.</summary>
    public const string TelemetryRetentionDays = "retention.telemetry-days";
}

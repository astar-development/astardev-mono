namespace AStarDev.CloudSync.Daemon;

/// <summary>Project-specific strings for the daemon, so none are repeated as literals.</summary>
internal static class ApplicationConstants
{
    public static string ServiceName => "astardev-cloudsync-daemon";

    public static string IpcConfigurationSection => "Ipc";

    public static string XdgRuntimeDirEnvironmentVariable => "XDG_RUNTIME_DIR";

    public static string SocketDirectoryName => "astardev-cloudsync";

    public static string SocketFileName => "daemon.sock";

    public static string DevelopmentConfigurationSection => "CloudSync";

    public static string UnknownVersion => "unknown";

    public static string DaemonVersion { get; } = typeof(ApplicationConstants).Assembly.GetName().Version?.ToString() ?? UnknownVersion;

    public static string ProcessStartStageName => "Process start";

    public static string InstanceNameSuffix => ".instance";

    public static string AlreadyRunningText => "Another CloudSync daemon is already running for this user. This one will not start.";

    public static string NotAnsweringText => "Another CloudSync daemon holds the instance lock but did not answer. This one will not start.";

    public static string InstanceLockUnavailableText => "The single-instance lock could not be created, so one daemon per user cannot be enforced. This one will not start.";

    public static string InfrastructureInitialisationStageName => "Infrastructure initialisation";

    public static string StateRecoveryStageName => "State recovery";

    public static string QueueRecoveryStageName => "Queue recovery";

    public static string BackgroundServicesStageName => "Background services";

    public static string AcceptConnectionsStageName => "Accept connections";

    public static string DriveRootItemId => "root";

    public static string OneDriveProviderType => "OneDrive";

    public static string ProviderHealthCheckName => "provider";

    public static string QueueHealthCheckName => "queue";

    public static string EntityFrameworkCategoryPrefix => "Microsoft.EntityFrameworkCore";

    public static string TelemetryBatchWriterCategory => "AStarDev.CloudSync.Persistence.TelemetryBatchWriter";

    public static string ActivitySourcePrefix => "AStarDev.CloudSync.";

    public static TimeSpan MetricSampleInterval => TimeSpan.FromMinutes(1);

    public static TimeSpan QueuePollInterval => TimeSpan.FromSeconds(2);

    public static int QueueListLimit => 500;

    public static TimeSpan StopReplyGrace => TimeSpan.FromMilliseconds(200);

    public static int RestartExitCode => 75;

    public static string InterruptedOperationReason => "Interrupted by a daemon restart.";
}

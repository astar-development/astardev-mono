namespace AStarDev.CloudSync.IPC;

/// <summary>
/// One thing the daemon is doing for a sync job, with every number already worked out. The UI shows these values as they are and never computes progress or time left.
/// </summary>
/// <param name="SyncJobId">The job the activity belongs to; a later report for the same job replaces this one.</param>
/// <param name="Name">The job's friendly name.</param>
/// <param name="State">Which trust state the activity is in.</param>
/// <param name="ItemsDone">Items processed so far, including those processed before a restart.</param>
/// <param name="ItemsTotal">Items to process in all; <see langword="null"/> when the provider cannot say, in which case <paramref name="Percent"/> and <paramref name="EtaSeconds"/> are also <see langword="null"/>.</param>
/// <param name="Percent">Share done, 0 to 100; <see langword="null"/> when <paramref name="ItemsTotal"/> is unknown.</param>
/// <param name="EtaSeconds">Estimated seconds left at the speed so far; <see langword="null"/> until there is enough progress to estimate from, and when <paramref name="ItemsTotal"/> is unknown.</param>
/// <param name="StartedUtc">When the daemon first reported this activity in this run.</param>
/// <param name="UpdatedUtc">When the daemon last reported progress; a long gap suggests stuck work.</param>
/// <param name="RetryInSeconds">How long the daemon waits before trying again; only set while <paramref name="State"/> is <see cref="ActivityState.Waiting"/>.</param>
/// <param name="ActionRequired"><see langword="true"/> when a folder stopped on an error and the job needs to be started again.</param>
public sealed record ActivityView(Guid SyncJobId, string Name, ActivityState State, long ItemsDone, long? ItemsTotal, double? Percent, int? EtaSeconds, DateTimeOffset StartedUtc, DateTimeOffset UpdatedUtc, int? RetryInSeconds, bool ActionRequired);

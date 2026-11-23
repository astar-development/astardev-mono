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
/// <param name="EtaSeconds">Estimated seconds left for the whole job at the speed of the last 60 seconds, all folders combined; <see langword="null"/> while <paramref name="State"/> is not <see cref="ActivityState.InitialIndexing"/> or <see cref="ActivityState.Working"/> (so never during a retry wait), when nothing moved in the window, and when <paramref name="ItemsTotal"/> is unknown.</param>
/// <param name="StartedUtc">When the daemon first reported this activity in this run.</param>
/// <param name="UpdatedUtc">When the daemon last reported progress; a long gap suggests stuck work.</param>
/// <param name="RetryInSeconds">How long the daemon waits before trying again; only set while <paramref name="State"/> is <see cref="ActivityState.Waiting"/>.</param>
/// <param name="ActionRequired"><see langword="true"/> when a folder stopped on an error and the job needs to be started again.</param>
/// <param name="ItemsPerSecond">Items per second over the same 60 second window as <paramref name="EtaSeconds"/>; <see langword="null"/> when the state is not <see cref="ActivityState.InitialIndexing"/> or <see cref="ActivityState.Working"/> or fewer than two measurements exist; it may be set when <paramref name="ItemsTotal"/> is unknown.</param>
public sealed record ActivityView(Guid SyncJobId, string Name, ActivityState State, long ItemsDone, long? ItemsTotal, double? Percent, int? EtaSeconds, DateTimeOffset StartedUtc, DateTimeOffset UpdatedUtc, int? RetryInSeconds, bool ActionRequired, double? ItemsPerSecond = null);

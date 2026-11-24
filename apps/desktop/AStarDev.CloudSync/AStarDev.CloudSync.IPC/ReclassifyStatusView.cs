namespace AStarDev.CloudSync.IPC;

/// <summary>Where the reclassify run stands, answering <see cref="GetReclassifyStatus"/>.</summary>
/// <param name="Running"><see langword="true"/> while a run is active.</param>
/// <param name="FilesDone">Files gone through so far in the active run, or in the last run when none is active.</param>
/// <param name="LastCompletedUtc">When the last run that finished ended; <see langword="null"/> when none ever finished.</param>
public sealed record ReclassifyStatusView(bool Running, long FilesDone, DateTimeOffset? LastCompletedUtc);

namespace AStarDev.CloudSync.Classification;

/// <summary>The run went through every file and was recorded.</summary>
/// <param name="FilesClassified">How many files it went through.</param>
public sealed record ReclassifyCompleted(long FilesClassified);

/// <summary>Another run is active, so nothing was started. Wait with <see cref="IClassificationReclassifier.WaitUntilIdleAsync"/> and ask again if a run that starts later is needed.</summary>
public sealed record ReclassifyAlreadyRunning;

/// <summary>The last recorded run used the applied seed, so nothing was started.</summary>
public sealed record ReclassifyNotNeeded;

/// <summary>The run stopped early and was not recorded, so the next start tries again.</summary>
/// <param name="Reason">Why it stopped, suitable for logs. Never holds a path or term.</param>
public sealed record ReclassifyFailed(string Reason);

/// <summary>How a reclassify call ended. A cancelled run is not an outcome: it propagates as <see cref="OperationCanceledException"/>.</summary>
public union ReclassifyOutcome(ReclassifyCompleted, ReclassifyAlreadyRunning, ReclassifyNotNeeded, ReclassifyFailed);

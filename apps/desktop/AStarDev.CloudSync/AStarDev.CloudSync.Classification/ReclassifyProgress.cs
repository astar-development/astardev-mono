namespace AStarDev.CloudSync.Classification;

/// <summary>How far a reclassify run has got.</summary>
/// <param name="FilesDone">The files gone through so far in this run.</param>
public sealed record ReclassifyProgress(long FilesDone);

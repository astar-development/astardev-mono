namespace AStarDev.CloudSync.Ports;

/// <summary>The last reclassify run that finished.</summary>
/// <param name="CompletedUtc">When it finished.</param>
/// <param name="FilesClassified">How many files it went through.</param>
/// <param name="SeedVersion">The version of the shipped seed that was applied when it ran, so a newer seed makes the run stale.</param>
public sealed record ReclassifyRun(DateTimeOffset CompletedUtc, long FilesClassified, string SeedVersion);

namespace AStarDev.CloudSync.Classification;

/// <summary>The seed was written to the stores and its version recorded.</summary>
internal sealed record SeedApplied;

/// <summary>The recorded version equals the seed version, so nothing was written.</summary>
internal sealed record SeedAlreadyCurrent;

/// <summary>The seed was not fully applied and its version was not recorded, so the next run tries again.</summary>
/// <param name="Reason">Why the run failed, suitable for logs. Never holds a path or term.</param>
internal sealed record SeedFailed(string Reason);

/// <summary>How a seeding run ended.</summary>
internal union SeedOutcome(SeedApplied, SeedAlreadyCurrent, SeedFailed);

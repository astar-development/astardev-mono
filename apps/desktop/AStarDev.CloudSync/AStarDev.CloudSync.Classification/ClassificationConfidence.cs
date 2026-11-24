namespace AStarDev.CloudSync.Classification;

/// <summary>How sure a signal is about a candidate.</summary>
internal enum ClassificationConfidence
{
    /// <summary>A clear match.</summary>
    High,

    /// <summary>An ambiguous match, for example a word that was only suggested and not reviewed.</summary>
    Low
}

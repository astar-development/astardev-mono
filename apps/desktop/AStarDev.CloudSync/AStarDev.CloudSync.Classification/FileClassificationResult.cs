using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>The candidates found for one file. No candidates means the file is unclassified.</summary>
/// <param name="Path">The classified path.</param>
/// <param name="Candidates">The candidates, one per term text and category.</param>
internal sealed record FileClassificationResult(RelativePath Path, IReadOnlyList<ClassificationCandidate> Candidates);

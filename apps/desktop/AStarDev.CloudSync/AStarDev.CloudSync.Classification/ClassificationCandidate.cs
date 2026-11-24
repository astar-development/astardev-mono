using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Classification;

/// <summary>One term a signal found in a path.</summary>
/// <param name="Term">The term found.</param>
/// <param name="Category">The term's category.</param>
/// <param name="Signal">The signal that found it.</param>
/// <param name="Confidence">How sure the signal is.</param>
internal sealed record ClassificationCandidate(Term Term, Category Category, SignalKind Signal, ClassificationConfidence Confidence);

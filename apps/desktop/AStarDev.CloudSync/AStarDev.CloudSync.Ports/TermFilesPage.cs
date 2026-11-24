namespace AStarDev.CloudSync.Ports;

/// <summary>One page of the files linked to a term.</summary>
/// <param name="Total">How many files the term has in all, so a caller can page.</param>
/// <param name="Files">The files of this page, ordered by path.</param>
public sealed record TermFilesPage(int Total, IReadOnlyList<TermFile> Files);

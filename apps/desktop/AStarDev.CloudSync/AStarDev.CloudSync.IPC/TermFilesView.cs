namespace AStarDev.CloudSync.IPC;

/// <summary>One page of the files linked to a term, answering <see cref="GetTermFiles"/>.</summary>
/// <param name="Total">How many files the term has in all. Zero when the term is unknown or has none.</param>
/// <param name="Files">The files of this page, ordered by path.</param>
public sealed record TermFilesView(int Total, IReadOnlyList<TermFileView> Files);

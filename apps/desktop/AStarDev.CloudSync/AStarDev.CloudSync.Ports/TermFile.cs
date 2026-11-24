using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>A file linked to a term, with where it is and which account holds it.</summary>
/// <param name="ItemId">Our identity for the file.</param>
/// <param name="Path">Where the file sits now below the job's local root.</param>
/// <param name="AccountName">The display name of the account the file belongs to.</param>
public sealed record TermFile(ItemId ItemId, RelativePath Path, string AccountName);

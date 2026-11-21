namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>One conflict as the Conflicts screen shows it.</summary>
/// <param name="Id">Stable identifier; the deep-link target other screens navigate with.</param>
/// <param name="Name">The affected item's file name.</param>
/// <param name="Age">How long the conflict has been open.</param>
/// <param name="Status">Whether it is still undecided or deferred.</param>
/// <param name="Reason">Why the conflict was created.</param>
/// <param name="Local">The local version.</param>
/// <param name="Remote">The remote version.</param>
internal sealed record ConflictItem(string Id, string Name, TimeSpan Age, ConflictStatus Status, ConflictReason Reason, ConflictVersion Local, ConflictVersion Remote);

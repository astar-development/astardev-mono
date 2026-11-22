namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The actions to apply, in order. Several steps are a merge of independent changes, such as a rename on one side and a content update on the other.</summary>
/// <param name="Steps">Never empty and never contains <see cref="SyncDecisionType.Conflict"/>. Exactly <see cref="SyncDecisionType.NoOp"/> alone when nothing is to be done. Renames come before updates.</param>
internal sealed record Resolved(IReadOnlyList<SyncDecisionType> Steps);

/// <summary>No single safe outcome exists, so nothing is applied and the item is surfaced to the user.</summary>
/// <param name="Reason">The competing intents.</param>
internal sealed record Conflicted(ConflictReason Reason);

/// <summary>What to do about one item: either apply a safe set of actions or surface a conflict.</summary>
internal union SyncDecision(Resolved, Conflicted);

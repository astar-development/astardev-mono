using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Decides what to do about one item from its known, local and remote state. Pure: no I/O, no state, safe to call concurrently.</summary>
internal interface IReconciler
{
    /// <summary>Resolves the state to a decision. Total: every state yields a decision. Ambiguity, content versus content and keep versus delete all yield <see cref="Conflicted"/>, never a guess and never an overwrite.</summary>
    /// <param name="state">The item's baseline and both current observations.</param>
    /// <returns>A <see cref="Resolved"/> with the actions to apply, or a <see cref="Conflicted"/>.</returns>
    SyncDecision Reconcile(CanonicalItemState state);
}

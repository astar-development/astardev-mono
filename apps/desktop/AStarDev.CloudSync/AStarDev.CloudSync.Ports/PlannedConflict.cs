using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>A conflict the planner found and the journal entry that records its detection.</summary>
/// <param name="Conflict">The conflict to store, as <see cref="IConflictStore.AddAsync"/> would: skipped when its item already has an open or deferred conflict of the same type.</param>
/// <param name="Detected">The journal entry appended only when the conflict is stored.</param>
public sealed record PlannedConflict(Conflict Conflict, JournalEntry Detected);

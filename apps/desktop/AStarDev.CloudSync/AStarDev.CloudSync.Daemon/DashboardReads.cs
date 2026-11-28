using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>What the dashboard reads from the state database, kept apart from the activity, rate and time inputs that move without a write so it can be reused while nothing is written.</summary>
/// <param name="ProviderHealth">The provider health check's status, which comes from the stored accounts.</param>
/// <param name="Counts">Operations in each status; a missing status has none.</param>
/// <param name="OpenConflicts">Conflicts waiting for the user.</param>
/// <param name="Accounts">Every stored account.</param>
/// <param name="Newest">The newest operation in any status, or none.</param>
/// <param name="Running">The running operations with their paths.</param>
/// <param name="ScopeEntries">The journal entries for folders added to or removed from a selection.</param>
/// <param name="Setup">How far the user got with accounts and folders.</param>
internal sealed record DashboardReads(HealthStatus ProviderHealth, IReadOnlyDictionary<OperationStatus, int> Counts, int OpenConflicts, IReadOnlyList<Account> Accounts, IReadOnlyList<Operation> Newest, IReadOnlyList<DashboardOperationView> Running, IReadOnlyList<JournalEntry> ScopeEntries, DashboardSetup Setup);

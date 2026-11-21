namespace AStarDev.CloudSync.IPC;

/// <summary>One sync job as the UI shows it.</summary>
/// <param name="Id">Identifies the job in <see cref="AddSyncScope"/>, <see cref="RemoveSyncScope"/> and <see cref="GetSyncScopes"/>.</param>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account the job synchronises.</param>
/// <param name="Name">Friendly name of the job.</param>
/// <param name="LocalRootPath">Absolute path of the local sync folder.</param>
/// <param name="BootstrapMode">The mode chosen when the job was created.</param>
public sealed record SyncJobView(Guid Id, Guid AccountId, string Name, string LocalRootPath, SyncBootstrapMode BootstrapMode);

namespace AStarDev.CloudSync.Domain;

/// <summary>A configured sync relationship between one account's remote folders and a local root.</summary>
/// <param name="Id">Our identity for the job.</param>
/// <param name="AccountId">The account the job synchronises.</param>
/// <param name="Name">Friendly name shown to the user.</param>
/// <param name="LocalRootPath">Absolute path of the local sync root. It exists when the job is created.</param>
/// <param name="RemoteRootId">The provider's id for the remote root the scopes sit under, which is the drive root.</param>
/// <param name="BootstrapMode">How the first synchronisation treats content that exists on both sides.</param>
/// <param name="Enabled">When <see langword="false"/> the job is kept but not synchronised.</param>
/// <param name="CreatedUtc">When the job was created.</param>
/// <param name="UpdatedUtc">When the job was last changed. Equals <paramref name="CreatedUtc"/> for a job never changed.</param>
public sealed record SyncJob(SyncJobId Id, AccountId AccountId, string Name, string LocalRootPath, RemoteItemId RemoteRootId, BootstrapMode BootstrapMode, bool Enabled, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);

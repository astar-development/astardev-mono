namespace AStarDev.CloudSync.IPC;

/// <summary>The figures of one account's card on the Providers screen, all worked out by the daemon.</summary>
/// <param name="AccountId">The account these figures belong to, as in <see cref="AccountView.Id"/>.</param>
/// <param name="LastSyncUtc">When a sync of this account last completed, kept across restarts; <see langword="null"/> when none has.</param>
/// <param name="StorageUsedBytes">Storage in use at the provider, in bytes; <see langword="null"/> when the provider could not say, or the account must sign in again.</param>
/// <param name="StorageTotalBytes">Storage the account is allowed at the provider, in bytes; <see langword="null"/> exactly when <paramref name="StorageUsedBytes"/> is.</param>
/// <param name="Pending">Operations of this account waiting to start.</param>
/// <param name="Running">Operations of this account executing now.</param>
/// <param name="Failed">Operations of this account that failed, retried ones and dead-lettered ones alike.</param>
/// <param name="Completed">Operations of this account that finished; with the other three it adds up to the dashboard's item total.</param>
public sealed record ProviderInsightsView(Guid AccountId, DateTimeOffset? LastSyncUtc, long? StorageUsedBytes, long? StorageTotalBytes, int Pending, int Running, int Failed, int Completed = 0);

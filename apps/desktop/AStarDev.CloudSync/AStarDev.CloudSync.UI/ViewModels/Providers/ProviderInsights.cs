namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Per-account figures the daemon does not report yet. Today they are labelled sample data.</summary>
/// <param name="Health">The health to show while sign-in is valid. Sign-in expiry always overrides it.</param>
/// <param name="LastSyncText">Ready-made last sync line.</param>
/// <param name="StorageText">Ready-made storage consumption line.</param>
/// <param name="RecentActivityText">Ready-made recent activity line.</param>
internal sealed record ProviderInsights(ProviderHealth Health, string LastSyncText, string StorageText, string RecentActivityText);

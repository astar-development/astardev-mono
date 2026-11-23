namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetProviders"/>.</summary>
/// <param name="Providers">What the daemon supports; one entry per provider type, which is OneDrive today.</param>
/// <param name="Accounts">One entry per connected account, oldest connection first, in the order of <see cref="AccountsView"/>.</param>
public sealed record ProvidersView(IReadOnlyList<ProviderView> Providers, IReadOnlyList<ProviderInsightsView> Accounts);

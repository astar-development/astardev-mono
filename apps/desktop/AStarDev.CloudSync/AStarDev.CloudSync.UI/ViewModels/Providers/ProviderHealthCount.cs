namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>One line of the health summary: a state and how many accounts are in it.</summary>
/// <param name="Health">The state, drawn with colour, icon and text.</param>
/// <param name="Count">Accounts in that state; zero when none, so every state stays visible.</param>
internal sealed record ProviderHealthCount(ProviderHealthViewModel Health, int Count);

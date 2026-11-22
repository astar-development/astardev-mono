using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Supplies the figures a card shows beyond what the account list carries. Replace the implementation when the daemon reports them.</summary>
internal interface IProviderInsightsSource
{
    /// <summary>The figures for <paramref name="account"/>. Never fails: unknown values come back as readable placeholder text.</summary>
    /// <param name="account">The account the card is for.</param>
    ProviderInsights Describe(AccountView account);
}

using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The permanent status bar. Shows the daemon's provider health, queue activity, error state and last activity while the daemon link is up, and unknown values, never the last ones, while it is down.</summary>
public sealed partial class StatusBarViewModel : ViewModelBase
{
    private readonly IStatusSource source;
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;

    internal StatusBarViewModel(IStatusSource source, ILocalisationService localisation, ICultureService culture)
    {
        this.source = source;
        this.localisation = localisation;
        this.culture = culture;
        ConnectionText = source.StatusText;
        ProviderHealth = Unknown();
        QueueActivityText = localisation.Get("StatusBar.QueueActivity.Unknown");
        ErrorStateText = localisation.Get("StatusBar.ErrorState.Unknown");
        LastActivityText = localisation.Get("StatusBar.LastActivity.Unknown");
        source.PropertyChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>The state of the daemon link as text, so it stays visible on every screen.</summary>
    [ObservableProperty]
    public partial string ConnectionText { get; private set; }

    /// <summary>The daemon's provider health, shown as colour, icon and text. Critical with unknown text when the link is down.</summary>
    [ObservableProperty]
    public partial StatusIndicatorViewModel ProviderHealth { get; private set; }

    /// <summary>How many operations wait or run, or unknown when the link is down.</summary>
    [ObservableProperty]
    public partial string QueueActivityText { get; private set; }

    /// <summary>How many operations wait for a decision, none, or unknown when the link is down.</summary>
    [ObservableProperty]
    public partial string ErrorStateText { get; private set; }

    /// <summary>The time of the latest sync progress, none before any, or unknown when the link is down.</summary>
    [ObservableProperty]
    public partial string LastActivityText { get; private set; }

    private void Refresh()
    {
        ConnectionText = source.StatusText;
        if (source.State == ConnectionState.Connected && source.SyncStatus.Value is Some<SyncStatusView> status) Show(status.Value);
        else ShowUnknown();
    }

    private void Show(SyncStatusView status)
    {
        var level = LevelOf(status.ProviderHealth);
        ProviderHealth = new StatusIndicatorViewModel(level, localisation.Format("StatusBar.ProviderHealth", ("state", localisation.Get($"Common.Trust.{level}"))));
        QueueActivityText = localisation.Format("StatusBar.QueueActivity", ("count", status.QueueDepth));
        ErrorStateText = status.ErrorCount == 0 ? localisation.Get("StatusBar.ErrorState.None") : localisation.Format("StatusBar.ErrorState", ("count", status.ErrorCount));
        LastActivityText = status.LastActivityAt is { } at ? localisation.Format("StatusBar.LastActivity", ("time", culture.FormatTime(at.ToLocalTime()))) : localisation.Get("StatusBar.LastActivity.None");
    }

    private void ShowUnknown()
    {
        ProviderHealth = Unknown();
        QueueActivityText = localisation.Get("StatusBar.QueueActivity.Unknown");
        ErrorStateText = localisation.Get("StatusBar.ErrorState.Unknown");
        LastActivityText = localisation.Get("StatusBar.LastActivity.Unknown");
    }

    private StatusIndicatorViewModel Unknown() => new(TrustLevel.Critical, localisation.Get("StatusBar.ProviderHealth.Unknown"));

    private static TrustLevel LevelOf(ProviderHealthState health) => health switch
    {
        ProviderHealthState.Healthy => TrustLevel.Healthy,
        ProviderHealthState.Degraded => TrustLevel.Degraded,
        _ => TrustLevel.Critical
    };
}

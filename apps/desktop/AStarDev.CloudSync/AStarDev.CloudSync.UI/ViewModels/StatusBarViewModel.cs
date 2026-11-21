using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The permanent status bar. Provider health, queue activity, error state and last activity are placeholders until the daemon supplies them; the daemon link state is real.</summary>
public sealed class StatusBarViewModel : ViewModelBase
{
    internal StatusBarViewModel(ConnectionViewModel connection, ILocalisationService localisation)
    {
        Connection = connection;
        ProviderHealth = new StatusIndicatorViewModel(TrustLevel.Degraded, localisation.Get("StatusBar.ProviderHealth"));
        QueueActivityText = localisation.Get("StatusBar.QueueActivity");
        ErrorStateText = localisation.Get("StatusBar.ErrorState");
        LastActivityText = localisation.Get("StatusBar.LastActivity");
    }

    /// <summary>The live daemon link, so its state stays visible on every screen.</summary>
    public ConnectionViewModel Connection { get; }

    /// <summary>Placeholder provider health, shown as colour, icon and text.</summary>
    public StatusIndicatorViewModel ProviderHealth { get; }

    /// <summary>Placeholder queue activity text.</summary>
    public string QueueActivityText { get; }

    /// <summary>Placeholder error state text.</summary>
    public string ErrorStateText { get; }

    /// <summary>Placeholder last activity text.</summary>
    public string LastActivityText { get; }
}

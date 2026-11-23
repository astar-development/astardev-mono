using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Screens;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Loads and applies the saved settings whenever the daemon link comes up, so the saved theme and notification preferences hold from the start. Constructing it starts the listening, so resolve it once at startup.</summary>
internal sealed class SettingsStartup
{
    /// <summary>Starts listening to the connection.</summary>
    /// <param name="connection">The settings are loaded each time it reaches <see cref="ConnectionState.Connected"/>.</param>
    /// <param name="screens">Every screen; the Settings screen among them is the one loaded.</param>
    public SettingsStartup(ConnectionViewModel connection, IEnumerable<IScreen> screens)
    {
        var settings = screens.OfType<SettingsViewModel>().Single();
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.State) && connection.State == ConnectionState.Connected) settings.LoadCommand.Execute(null);
        };
    }
}

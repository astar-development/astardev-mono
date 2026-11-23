using System.ComponentModel;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Where the status bar gets its values: the daemon link and the status the daemon last reported over IPC. Raises <see cref="INotifyPropertyChanged.PropertyChanged"/> when either changes.</summary>
internal interface IStatusSource : INotifyPropertyChanged
{
    /// <summary>The state of the daemon link. Only <see cref="ConnectionState.Connected"/> makes <see cref="SyncStatus"/> current.</summary>
    public ConnectionState State { get; }

    /// <summary>Text for <see cref="State"/>.</summary>
    public string StatusText { get; }

    /// <summary>The status the daemon last reported. Empty before the first report; kept after the link drops, so read it with <see cref="State"/>.</summary>
    public Option<SyncStatusView> SyncStatus { get; }
}

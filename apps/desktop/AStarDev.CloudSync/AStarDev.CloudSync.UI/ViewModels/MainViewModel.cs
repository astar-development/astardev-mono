namespace AStarDev.CloudSync.UI.ViewModels;

public sealed class MainViewModel(ConnectionViewModel connection) : ViewModelBase
{
    public ConnectionViewModel Connection { get; } = connection;
}

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>.</summary>
/// <param name="connection">The app-wide daemon connection, shared rather than created here so its retry loop outlives any one view.</param>
/// <param name="connect">The account connection, kept here so its result survives the view being rebuilt.</param>
/// <param name="accounts">The account cards. Refreshed whenever the daemon link comes up and whenever a connect attempt finishes.</param>
public sealed class MainViewModel : ViewModelBase
{
    /// <summary>Creates the view model and starts listening for the moments the account list may have changed.</summary>
    public MainViewModel(ConnectionViewModel connection, ConnectViewModel connect, AccountsViewModel accounts)
    {
        Connection = connection;
        Connect = connect;
        Accounts = accounts;
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.State) && connection.State == ConnectionState.Connected) accounts.RefreshCommand.Execute(null);
        };
        connect.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectViewModel.IsBusy) && !connect.IsBusy) accounts.RefreshCommand.Execute(null);
        };
    }

    /// <summary>Daemon link state for the status banner.</summary>
    public ConnectionViewModel Connection { get; }

    /// <summary>Connect button, busy state, item count and errors.</summary>
    public ConnectViewModel Connect { get; }

    /// <summary>The connected accounts with their Disconnect and Reconnect buttons.</summary>
    public AccountsViewModel Accounts { get; }
}

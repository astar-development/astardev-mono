namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>.</summary>
public sealed class MainViewModel : ViewModelBase
{
    /// <summary>Creates the view model and starts listening for the moments the account list and folder tree may have changed.</summary>
    /// <param name="connection">The app-wide daemon connection, shared rather than created here so its retry loop outlives any one view.</param>
    /// <param name="connect">The account connection, kept here so its result survives the view being rebuilt.</param>
    /// <param name="accounts">The account cards. Refreshed whenever the daemon link comes up and whenever a connect attempt finishes.</param>
    /// <param name="folders">The remote folder tree. Listed afresh whenever the stored selection has been read and whenever a connect attempt succeeds.</param>
    /// <param name="selection">The ticked folders, local folder and job. Read whenever the daemon link comes up, which then lists the tree so the stored ticks show.</param>
    public MainViewModel(ConnectionViewModel connection, ConnectViewModel connect, AccountsViewModel accounts, FolderTreeViewModel folders, ScopeSelectionViewModel selection)
    {
        Connection = connection;
        Connect = connect;
        Accounts = accounts;
        Folders = folders;
        Selection = selection;
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.State) && connection.State == ConnectionState.Connected) RefreshAll();
        };
        connect.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(ConnectViewModel.IsBusy) || connect.IsBusy) return;

            accounts.RefreshCommand.Execute(null);
            if (connect.ErrorMessage is null) folders.RefreshCommand.Execute(null);
        };

        selection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScopeSelectionViewModel.IsLoading) && !selection.IsLoading) folders.RefreshCommand.Execute(null);
        };

        void RefreshAll()
        {
            accounts.RefreshCommand.Execute(null);
            selection.RefreshCommand.Execute(null);
        }
    }

    /// <summary>Daemon link state for the status banner.</summary>
    public ConnectionViewModel Connection { get; }

    /// <summary>Connect button, busy state, item count and errors.</summary>
    public ConnectViewModel Connect { get; }

    /// <summary>The connected accounts with their Disconnect and Reconnect buttons.</summary>
    public AccountsViewModel Accounts { get; }

    /// <summary>The remote folders, expanded level by level.</summary>
    public FolderTreeViewModel Folders { get; }

    /// <summary>The ticked folders with the local folder, bootstrap mode and Create job button.</summary>
    public ScopeSelectionViewModel Selection { get; }
}

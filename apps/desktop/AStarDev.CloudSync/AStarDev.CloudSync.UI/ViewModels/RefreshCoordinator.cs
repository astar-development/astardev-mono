namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Refreshes the account list and folder tree at the moments they may have changed. Constructing it starts the listening, so resolve it once at startup.</summary>
internal sealed class RefreshCoordinator
{
    /// <summary>Starts listening to the connection, the connect attempt and the scope selection.</summary>
    /// <param name="connection">Account list and stored selection are refreshed whenever the daemon link comes up.</param>
    /// <param name="connect">Accounts are refreshed whenever a connect attempt finishes, and the folder tree too when it succeeded.</param>
    /// <param name="accounts">The account cards.</param>
    /// <param name="folders">The remote folder tree. Listed afresh whenever the stored selection has been read.</param>
    /// <param name="selection">The ticked folders, local folder and job.</param>
    public RefreshCoordinator(ConnectionViewModel connection, ConnectViewModel connect, AccountsViewModel accounts, FolderTreeViewModel folders, ScopeSelectionViewModel selection)
    {
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
}

using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>
/// Which remote folders are ticked, where they sync to locally and the sync job that records the choice. Before a job exists the ticks are only held here;
/// once it exists every tick or untick is sent to the daemon at once, so the selection survives a restart of either side.
/// </summary>
/// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
/// <param name="picker">Asks the user for a local folder when Browse is pressed.</param>
public sealed partial class ScopeSelectionViewModel(Func<IIpcClient> createClient, IFolderPicker picker) : ViewModelBase
{
    private string localRoot = string.Empty;
    private SyncBootstrapMode bootstrapMode = SyncBootstrapMode.Merge;
    private SyncJobView? job;
    private string? statusMessage;
    private bool isLoading;
    private bool isBusy;

    /// <summary>The ticked folders, as the provider's ids, in the order they were ticked or listed. Empty until the user ticks something or a stored job is loaded.</summary>
    public ObservableCollection<string> SelectedFolderIds { get; } = [];

    /// <summary>Names of the local top-level folders that no scope covers. They are not synced, so nothing in them is uploaded or deleted. Empty until a stored job is loaded.</summary>
    public ObservableCollection<string> UnsyncedFolders { get; } = [];

    /// <summary>True when <see cref="UnsyncedFolders"/> holds a folder, so the view shows its heading only then.</summary>
    public bool HasUnsyncedFolders => UnsyncedFolders.Count > 0;

    /// <summary>The bootstrap modes the user can pick from.</summary>
    public IReadOnlyList<SyncBootstrapMode> BootstrapModes { get; } = [SyncBootstrapMode.Download, SyncBootstrapMode.Upload, SyncBootstrapMode.Merge];

    /// <summary>True once the daemon holds a sync job. The MVP allows one, so the form is then read-only.</summary>
    public bool HasJob => job is not null;

    /// <summary>The local folder the job syncs to, typed or picked. After a job exists it shows the job's folder.</summary>
    public string LocalRoot
    {
        get => localRoot;
        set
        {
            if (SetProperty(ref localRoot, value)) OnPropertyChanged(nameof(CanCreateJob));
        }
    }

    /// <summary>How the first synchronisation treats content on both sides. Defaults to <see cref="SyncBootstrapMode.Merge"/>, which discards nothing.</summary>
    public SyncBootstrapMode BootstrapMode
    {
        get => bootstrapMode;
        set => SetProperty(ref bootstrapMode, value);
    }

    /// <summary>One line describing the stored job, empty while there is none.</summary>
    public string JobSummary => job is null ? string.Empty : string.Format(ApplicationConstants.JobSummaryFormat, job.LocalRootPath, job.BootstrapMode);

    /// <summary>True when no job exists yet, a local folder is given, at least one folder is ticked and no request is running.</summary>
    public bool CanCreateJob => job is null && !isBusy && !string.IsNullOrWhiteSpace(localRoot) && SelectedFolderIds.Count > 0;

    /// <summary>Why the last request failed, worded for the user. <see langword="null"/> after a request that worked.</summary>
    public string? StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    /// <summary>True while the stored job and selection are being read. Goes back to <see langword="false"/> when the read has finished or failed.</summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    /// <summary>True while a job is being created. The daemon may take a moment to answer.</summary>
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value)) OnPropertyChanged(nameof(CanCreateJob));
        }
    }

    /// <summary>Tells whether a folder is ticked.</summary>
    /// <param name="folderId">The provider's id of the folder.</param>
    /// <returns><see langword="true"/> when the folder is in <see cref="SelectedFolderIds"/>.</returns>
    public bool IsSelected(string folderId) => SelectedFolderIds.Contains(folderId);

    /// <summary>Ticks or unticks a folder. Does nothing when the folder is already in that state. When a job exists the change is also sent to the daemon.</summary>
    /// <param name="folderId">The provider's id of the folder.</param>
    /// <param name="selected"><see langword="true"/> to tick, <see langword="false"/> to untick.</param>
    public void Select(string folderId, bool selected)
    {
        if (selected == IsSelected(folderId)) return;

        if (selected) SelectedFolderIds.Add(folderId);
        else SelectedFolderIds.Remove(folderId);
        OnPropertyChanged(nameof(CanCreateJob));
        if (job is not null) ApplyCommand.Execute(new ScopeChange(folderId, selected));
    }

    /// <summary>Asks the user for a local folder and puts it in <see cref="LocalRoot"/>. A dismissed dialog leaves <see cref="LocalRoot"/> as it was. Also exposed to the view as <c>BrowseCommand</c>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the dialog and surfaces as <see cref="OperationCanceledException"/>.</param>
    [RelayCommand]
    public async Task BrowseAsync(CancellationToken cancellationToken)
    {
        var chosen = await picker.PickFolderAsync(cancellationToken);
        if (chosen is not null) LocalRoot = chosen;
    }

    /// <summary>
    /// Reads the stored job and its folders. With a job, shows its folder and mode and replaces the ticks with the stored ones; without one, leaves the ticks as they are.
    /// Failures land in <see cref="StatusMessage"/>; cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>RefreshCommand</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            await using var client = createClient();

            switch (await ConnectAsync(client, cancellationToken) switch
            {
                Ok<Unit> => await LoadAsync(client, cancellationToken),
                Fail<ConnectFailure> fail => fail
            })
            {
                case Ok<StoredSelection> ok:
                    Show(ok.Value);
                    break;
                case Fail<ConnectFailure> fail:
                    StatusMessage = fail.Error.Message;
                    break;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Creates the sync job for the first account with <see cref="LocalRoot"/> and <see cref="BootstrapMode"/>, then adds every ticked folder to it. The daemon checks the local folder.
    /// Failures land in <see cref="StatusMessage"/>; a failure while adding folders leaves the job in place. Cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Also exposed to the view as <c>CreateJobCommand</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    [RelayCommand]
    public async Task CreateJobAsync(CancellationToken cancellationToken)
    {
        if (!CanCreateJob) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            await using var client = createClient();

            switch (await ConnectAsync(client, cancellationToken) switch
            {
                Ok<Unit> => await CreateAsync(client, cancellationToken),
                Fail<ConnectFailure> fail => fail
            })
            {
                case Ok<CreatedJob> ok:
                    Show(new StoredSelection(ok.Value.Job, [.. SelectedFolderIds], []));
                    StatusMessage = ok.Value.ScopeFailure;
                    break;
                case Fail<ConnectFailure> fail:
                    StatusMessage = fail.Error.Message;
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sends one tick or untick to the daemon. Does nothing while no job exists. A refusal lands in <see cref="StatusMessage"/> and the ticks are read back from the daemon, so they never show a state it does not hold.
    /// Calls may overlap, so quick successive ticks are all sent. Also exposed to the view as <c>ApplyCommand</c>.
    /// </summary>
    /// <param name="change">The folder and whether it was ticked.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task ApplyAsync(ScopeChange change, CancellationToken cancellationToken)
    {
        if (job is not { } current) return;

        await using var client = createClient();

        if (await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await SendScopeChangeAsync(client, current, change, cancellationToken),
            Fail<ConnectFailure> fail => fail
        } is Fail<ConnectFailure> failure)
        {
            await RefreshAsync(cancellationToken);
            StatusMessage = failure.Error.Message;
        }
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<StoredSelection, ConnectFailure>> LoadAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetSyncJobs(), cancellationToken) switch
        {
            Ok<SyncJobsView> { Value.Jobs: [var stored, ..] } => await LoadScopesAsync(client, stored, cancellationToken),
            Ok<SyncJobsView> => new Ok<StoredSelection>(new StoredSelection(null, [], [])),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<StoredSelection, ConnectFailure>> LoadScopesAsync(IIpcClient client, SyncJobView stored, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetSyncScopes(stored.Id), cancellationToken) switch
        {
            Ok<SyncScopesView> ok => new Ok<StoredSelection>(new StoredSelection(stored, [.. ok.Value.Scopes.Select(scope => scope.RemoteRootId)], ok.Value.UnsyncedFolders)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<CreatedJob, ConnectFailure>> CreateAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetAccounts(), cancellationToken) switch
        {
            Ok<AccountsView> { Value.Accounts: [var account, ..] } => await SendCreateAsync(client, account, cancellationToken),
            Ok<AccountsView> => new Fail<ConnectFailure>(new ConnectFailure(ApplicationConstants.ConnectAccountFirstText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<CreatedJob, ConnectFailure>> SendCreateAsync(IIpcClient client, AccountView account, CancellationToken cancellationToken)
        => await client.SendAsync(new CreateSyncJob(account.Id, localRoot.Trim(), bootstrapMode), cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => await FindJobAsync(client, cancellationToken),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(new ConnectFailure(rejected.Value.Message ?? ApplicationConstants.RequestRejectedText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<CreatedJob, ConnectFailure>> FindJobAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetSyncJobs(), cancellationToken) switch
        {
            Ok<SyncJobsView> { Value.Jobs: [var created, ..] } => await AddScopesAsync(client, created, cancellationToken),
            Ok<SyncJobsView> => new Fail<ConnectFailure>(new ConnectFailure(ApplicationConstants.JobNotFoundText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<CreatedJob, ConnectFailure>> AddScopesAsync(IIpcClient client, SyncJobView created, CancellationToken cancellationToken)
    {
        foreach (var folderId in SelectedFolderIds.ToList())
        {
            if (await SendScopeChangeAsync(client, created, new ScopeChange(folderId, true), cancellationToken) is Fail<ConnectFailure> failure) return new Ok<CreatedJob>(new CreatedJob(created, failure.Error.Message));
        }

        return new Ok<CreatedJob>(new CreatedJob(created, null));
    }

    private static async Task<Result<Unit, ConnectFailure>> SendScopeChangeAsync(IIpcClient client, SyncJobView target, ScopeChange change, CancellationToken cancellationToken)
        => await client.SendAsync(change.Selected ? new AddSyncScope(target.Id, change.FolderId) : new RemoveSyncScope(target.Id, change.FolderId), cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => new Ok<Unit>(default),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(new ConnectFailure(rejected.Value.Message ?? ApplicationConstants.RequestRejectedText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(StoredSelection stored)
    {
        job = stored.Job;
        if (stored.Job is not null)
        {
            LocalRoot = stored.Job.LocalRootPath;
            BootstrapMode = stored.Job.BootstrapMode;
            SelectedFolderIds.Clear();
            foreach (var folderId in stored.FolderIds) SelectedFolderIds.Add(folderId);
            UnsyncedFolders.Clear();
            foreach (var name in stored.UnsyncedFolders) UnsyncedFolders.Add(name);
            OnPropertyChanged(nameof(HasUnsyncedFolders));
        }

        OnPropertyChanged(nameof(HasJob));
        OnPropertyChanged(nameof(JobSummary));
        OnPropertyChanged(nameof(CanCreateJob));
    }

    private sealed record StoredSelection(SyncJobView? Job, IReadOnlyList<string> FolderIds, IReadOnlyList<string> UnsyncedFolders);

    private sealed record CreatedJob(SyncJobView Job, string? ScopeFailure);
}

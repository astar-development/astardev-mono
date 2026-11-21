using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Connects an account through the daemon and shows how many items the drive root holds.</summary>
/// <param name="createClient">Creates a fresh client per attempt; one client is one connection.</param>
public sealed partial class ConnectViewModel(Func<IIpcClient> createClient) : ViewModelBase
{
    /// <summary>True from the moment connecting starts until it has finished or failed. The daemon is waiting for the user's browser sign-in during this time.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Files and folders directly under the drive root after the last successful connect. <see langword="null"/> until then, and again after a failed attempt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemCountText))]
    public partial int? ItemCount { get; set; }

    /// <summary>Why the last attempt failed, worded for the user. <see langword="null"/> while an attempt runs and after a successful one.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Ready-made label for <see cref="ItemCount"/>, empty while there is no count.</summary>
    public string ItemCountText => ItemCount switch
    {
        null => string.Empty,
        1 => ApplicationConstants.OneItemText,
        var count => string.Format(ApplicationConstants.ItemCountFormat, count)
    };

    /// <summary>
    /// Connects and then reads the count. Failures land in <see cref="ErrorMessage"/>; cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Also exposed to the view as <c>ConnectCommand</c>, which cannot run again while an attempt is in progress.
    /// </summary>
    [RelayCommand]
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        ItemCount = null;

        try
        {
            switch (await ConnectAndCountAsync(cancellationToken))
            {
                case Ok<RemoteRootSummaryView> ok:
                    ItemCount = ok.Value.ItemCount;
                    break;
                case Fail<ConnectFailure> fail:
                    ErrorMessage = fail.Error.Message;
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<Result<RemoteRootSummaryView, ConnectFailure>> ConnectAndCountAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        return await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await ConnectAccountAsync(client, cancellationToken),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };
    }

    private static async Task<Result<RemoteRootSummaryView, ConnectFailure>> ConnectAccountAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.SendAsync(new ConnectAccount(), cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => await CountAsync(client, cancellationToken),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(new ConnectFailure(rejected.Value.Message ?? ApplicationConstants.ConnectRejectedText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<RemoteRootSummaryView, ConnectFailure>> CountAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetRemoteRootSummary(), cancellationToken) switch
        {
            Ok<RemoteRootSummaryView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };
}

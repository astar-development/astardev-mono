using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The connected accounts, each as a card with Disconnect and, when sign-in expired, Reconnect.</summary>
/// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
public sealed partial class AccountsViewModel(Func<IIpcClient> createClient) : ViewModelBase
{
    /// <summary>The cards, oldest connection first. Replaced as a whole by each successful refresh and left as they were by a failed one.</summary>
    public ObservableCollection<AccountCardViewModel> Accounts { get; } = [];

    /// <summary>True while a Disconnect or Reconnect is waiting for the daemon. A reconnect waits for the user's browser sign-in, so this can last a while.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Why the last request failed, worded for the user. <see langword="null"/> after a request that worked.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>The daemon trace that handled the last failed request. <see langword="null"/> when it sent none and after a request that worked.</summary>
    [ObservableProperty]
    public partial string? ErrorTraceId { get; set; }

    /// <summary>
    /// Asks the daemon for the accounts. Failures land in <see cref="ErrorMessage"/>; cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Also exposed to the view as <c>RefreshCommand</c>.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        Show(await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListAsync(client, cancellationToken),
            Fail<ConnectFailure> fail => fail
        });
    }

    /// <summary>Asks the daemon to forget an account, then shows the accounts that remain. A refusal lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="account">The account to disconnect.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    public Task DisconnectAsync(AccountView account, CancellationToken cancellationToken) => SendAsync(new DisconnectAccount(account.Id), cancellationToken);

    /// <summary>Asks the daemon to sign in again through the browser, then shows the accounts. A refusal lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    public Task ReconnectAsync(CancellationToken cancellationToken) => SendAsync(new ConnectAccount(), cancellationToken);

    private async Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            await using var client = createClient();

            Show(await ConnectAsync(client, cancellationToken) switch
            {
                Ok<Unit> => await SendAndListAsync(client, command, cancellationToken),
                Fail<ConnectFailure> fail => fail
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<AccountsView, ConnectFailure>> SendAndListAsync(IIpcClient client, ICommand command, CancellationToken cancellationToken)
        => await client.SendAsync(command, cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => await ListAsync(client, cancellationToken),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(ConnectFailure.Rejected(rejected.Value, ApplicationConstants.RequestRejectedText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<AccountsView, ConnectFailure>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetAccounts(), cancellationToken) switch
        {
            Ok<AccountsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<AccountsView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<AccountsView> ok:
                Accounts.Clear();
                foreach (var account in ok.Value.Accounts) Accounts.Add(new AccountCardViewModel(account, this));
                ErrorMessage = null;
                ErrorTraceId = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorTraceId = fail.Error.TraceId.Match(id => (string?)id, () => null);
                ErrorMessage = fail.Error.Message;
                break;
        }
    }
}

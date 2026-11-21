using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class AccountsViewModelShould
{
    private static readonly AccountView Connected = new(Guid.NewGuid(), "ada@example.com", "OneDrive", AccountState.Connected);
    private static readonly AccountView Expired = new(Guid.NewGuid(), "grace@example.com", "OneDrive", AccountState.ReauthenticationRequired);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task offer_reconnect_when_reauthentication_is_required()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = Accounts(Connected, Expired) };
        var viewModel = new AccountsViewModel(() => client);

        await viewModel.RefreshAsync(Token);

        viewModel.Accounts.Select(card => card.DisplayName).ShouldBe(["ada@example.com", "grace@example.com"]);
        viewModel.Accounts[0].CanReconnect.ShouldBeFalse();
        viewModel.Accounts[1].CanReconnect.ShouldBeTrue();
        viewModel.Accounts[1].StatusText.ShouldBe("Sign in again to resume syncing");
        viewModel.Accounts[0].StatusText.ShouldBe("Connected");
    }

    [Fact]
    public async Task show_no_accounts_when_none_is_connected()
    {
        var viewModel = new AccountsViewModel(() => new FakeIpcClient([], available: true));

        await viewModel.RefreshAsync(Token);

        viewModel.Accounts.ShouldBeEmpty();
        viewModel.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task send_disconnect_for_the_account_and_refresh()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = Accounts(Connected) };
        var viewModel = new AccountsViewModel(() => client);
        await viewModel.RefreshAsync(Token);

        await viewModel.Accounts[0].DisconnectCommand.ExecuteAsync(null);

        client.Sent.ShouldHaveSingleItem().ShouldBe(new DisconnectAccount(Connected.Id));
    }

    [Fact]
    public async Task send_connect_when_reconnecting()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = Accounts(Expired) };
        var viewModel = new AccountsViewModel(() => client);
        await viewModel.RefreshAsync(Token);

        await viewModel.Accounts[0].ReconnectCommand.ExecuteAsync(null);

        client.Sent.ShouldHaveSingleItem().ShouldBeOfType<ConnectAccount>();
    }

    [Fact]
    public async Task show_the_daemon_message_when_disconnecting_is_rejected()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = Accounts(Connected), CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "The keyring is unavailable.")) };
        var viewModel = new AccountsViewModel(() => client);
        await viewModel.RefreshAsync(Token);

        await viewModel.Accounts[0].DisconnectCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.ShouldBe("The keyring is unavailable.");
        viewModel.Accounts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task say_the_daemon_cannot_be_reached_when_it_is_not_running()
    {
        var viewModel = new AccountsViewModel(() => new FakeIpcClient([], available: false));

        await viewModel.RefreshAsync(Token);

        viewModel.ErrorMessage.ShouldNotBeNull().ShouldContain("daemon");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var viewModel = new AccountsViewModel(() => new FakeIpcClient([], available: true));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.RefreshAsync(cancelled.Token));
    }

    private static Ok<AccountsView> Accounts(params AccountView[] accounts) => new(new AccountsView(accounts));
}

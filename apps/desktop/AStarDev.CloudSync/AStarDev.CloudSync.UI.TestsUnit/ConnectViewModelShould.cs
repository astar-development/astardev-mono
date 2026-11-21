using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ConnectViewModelShould
{
    private const string KeyringMessage = "The Secret Service keyring is unavailable, so the OneDrive sign-in cannot be stored.";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task show_the_item_count_after_connecting()
    {
        var client = new FakeIpcClient([], available: true) { SummaryReply = new Ok<RemoteRootSummaryView>(new RemoteRootSummaryView(42)) };
        var viewModel = new ConnectViewModel(() => client);

        await viewModel.ConnectAsync(Token);

        viewModel.ItemCount.ShouldBe(42);
        viewModel.ItemCountText.ShouldBe("42 items in the drive root");
        viewModel.ErrorMessage.ShouldBeNull();
        viewModel.IsBusy.ShouldBeFalse();
        client.Sent.ShouldHaveSingleItem().ShouldBeOfType<ConnectAccount>();
    }

    [Fact]
    public async Task explain_when_the_keyring_is_unavailable()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, KeyringMessage)) };
        var viewModel = new ConnectViewModel(() => client);

        await viewModel.ConnectAsync(Token);

        viewModel.ErrorMessage.ShouldBe(KeyringMessage);
        viewModel.ItemCount.ShouldBeNull();
        viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task not_ask_for_the_count_when_connecting_is_rejected()
    {
        var calls = new List<string>();
        var client = new FakeIpcClient(calls, available: true) { CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "denied")) };
        var viewModel = new ConnectViewModel(() => client);

        await viewModel.ConnectAsync(Token);

        calls.ShouldNotContain("query");
    }

    [Fact]
    public async Task be_busy_while_the_daemon_signs_in()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new ConnectViewModel(() => new FakeIpcClient([], available: true) { HoldCommand = hold });

        var connecting = viewModel.ConnectAsync(Token);
        var busyWhileWaiting = viewModel.IsBusy;
        hold.SetResult();
        await connecting;

        busyWhileWaiting.ShouldBeTrue();
        viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task clear_the_previous_error_when_connecting_again()
    {
        var viewModel = new ConnectViewModel(() => new FakeIpcClient([], available: false));
        await viewModel.ConnectAsync(Token);
        viewModel.ErrorMessage.ShouldNotBeNull();
        var succeeding = new ConnectViewModel(() => new FakeIpcClient([], available: true));

        await succeeding.ConnectAsync(Token);

        succeeding.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task say_the_daemon_cannot_be_reached_when_it_is_not_running()
    {
        var viewModel = new ConnectViewModel(() => new FakeIpcClient([], available: false));

        await viewModel.ConnectAsync(Token);

        viewModel.ErrorMessage.ShouldNotBeNull().ShouldContain("daemon");
        viewModel.ItemCount.ShouldBeNull();
    }

    [Fact]
    public async Task show_the_daemon_message_when_the_count_cannot_be_read()
    {
        var client = new FakeIpcClient([], available: true) { SummaryReply = new Fail<IpcError>(new HandlerFailed("Sign in first.")) };
        var viewModel = new ConnectViewModel(() => client);

        await viewModel.ConnectAsync(Token);

        viewModel.ErrorMessage.ShouldBe("Sign in first.");
        viewModel.ItemCount.ShouldBeNull();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var viewModel = new ConnectViewModel(() => new FakeIpcClient([], available: true));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.ConnectAsync(cancelled.Token));

        viewModel.IsBusy.ShouldBeFalse();
    }
}

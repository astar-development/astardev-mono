using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Providers;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class RemoveProviderViewModelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task require_explicit_confirmation()
    {
        var account = ProviderTestHost.Account("ada@example.com");
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(account));
        await host.Accounts.RefreshAsync(Token);
        host.Providers.Cards[0].RemoveCommand.Execute(null);
        var removal = host.Providers.Removal.ShouldNotBeNull();

        removal.ConfirmCommand.CanExecute(null).ShouldBeFalse();
        await removal.ConfirmCommand.ExecuteAsync(null);
        host.Client.Sent.ShouldBeEmpty();

        removal.IsConfirmed = true;
        removal.ConfirmCommand.CanExecute(null).ShouldBeTrue();
        await removal.ConfirmCommand.ExecuteAsync(null);

        host.Client.Sent.ShouldHaveSingleItem().ShouldBe(new DisconnectAccount(account.Id));
        host.Providers.Removal.ShouldBeNull();
    }

    [Fact]
    public async Task explain_that_files_are_kept_and_how_to_recover()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("ada@example.com")));
        await host.Accounts.RefreshAsync(Token);
        host.Providers.Cards[0].RemoveCommand.Execute(null);

        var removal = host.Providers.Removal.ShouldNotBeNull();

        removal.Title.ShouldContain("ada@example.com");
        removal.KeepsFilesText.ShouldNotStartWith("!");
        removal.RecoveryText.ShouldNotStartWith("!");
    }

    [Fact]
    public async Task leave_the_account_alone_when_cancelled()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("ada@example.com")));
        await host.Accounts.RefreshAsync(Token);
        host.Providers.Cards[0].RemoveCommand.Execute(null);

        host.Providers.Removal!.CancelCommand.Execute(null);

        host.Providers.Removal.ShouldBeNull();
        host.Client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var removal = new RemoveProviderViewModel("ada", _ => Task.CompletedTask, () => { }, "keeps", "recovery") { IsConfirmed = true };
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => removal.ConfirmAsync(cancelled.Token));
    }
}

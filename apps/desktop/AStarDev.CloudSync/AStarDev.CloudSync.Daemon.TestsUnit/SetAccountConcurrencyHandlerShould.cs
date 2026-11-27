using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class SetAccountConcurrencyHandlerShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public async Task store_an_allowed_value(int value)
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.Connected, Token);

        var result = await host.Services.GetRequiredService<SetAccountConcurrencyHandler>().HandleAsync(new SetAccountConcurrency(account.Id.Value, value), Token);
        var stored = await host.Services.GetRequiredService<IAccountStore>().GetAsync(account.Id, Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        stored.Value.ShouldBeOfType<Ok<Account>>().Value.MaxConcurrentOperations.ShouldBe(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(9)]
    public async Task reject_a_value_that_is_not_allowed_and_keep_the_old_one(int value)
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.Connected, Token);

        var result = await host.Services.GetRequiredService<SetAccountConcurrencyHandler>().HandleAsync(new SetAccountConcurrency(account.Id.Value, value), Token);
        var stored = await host.Services.GetRequiredService<IAccountStore>().GetAsync(account.Id, Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("1, 2, 4, 6 or 8");
        stored.Value.ShouldBeOfType<Ok<Account>>().Value.MaxConcurrentOperations.ShouldBe(4);
    }

    [Fact]
    public async Task reject_an_unknown_account()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<SetAccountConcurrencyHandler>().HandleAsync(new SetAccountConcurrency(Guid.NewGuid(), 2), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Rejected);
    }

    [Fact]
    public async Task list_the_changed_value_with_the_account()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.Connected, Token);

        await host.Services.GetRequiredService<SetAccountConcurrencyHandler>().HandleAsync(new SetAccountConcurrency(account.Id.Value, 6), Token);
        var listed = await host.Services.GetRequiredService<AccountsQuery>().HandleAsync(new GetAccounts(), Token);

        listed.Value.ShouldBeOfType<Ok<AccountsView>>().Value.Accounts.Single().MaxConcurrentOperations.ShouldBe(6);
    }

    [Fact]
    public async Task answer_the_command_over_ipc()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new SetAccountConcurrency(account.Id.Value, 2), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.Connected, Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SetAccountConcurrencyHandler>().HandleAsync(new SetAccountConcurrency(account.Id.Value, 2), cancelled.Token));
    }
}

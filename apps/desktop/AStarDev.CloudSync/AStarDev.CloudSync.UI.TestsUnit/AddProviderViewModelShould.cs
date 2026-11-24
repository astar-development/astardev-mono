using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Providers;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class AddProviderViewModelShould
{
    private static AddProviderStep[] AllSteps => Enum.GetValues<AddProviderStep>();

    [Fact]
    public void define_the_seven_steps_of_the_journey_in_order() => AllSteps.ShouldBe([AddProviderStep.SelectProvider, AddProviderStep.Authenticate, AddProviderStep.GrantPermissions, AddProviderStep.VerifyAccess, AddProviderStep.ChooseSyncRoot, AddProviderStep.ReviewConfiguration, AddProviderStep.Activate]);

    [Fact]
    public async Task walk_every_step_reusing_the_connect_flow_then_close()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith());
        var add = host.Providers.Add;
        add.BeginCommand.Execute(null);
        add.IsOpen.ShouldBeTrue();
        add.Step.ShouldBe(AddProviderStep.SelectProvider);
        add.ContinueCommand.CanExecute(null).ShouldBeFalse();

        add.SelectCommand.Execute(add.Providers[0]);
        var visited = new List<AddProviderStep> { add.Step };
        while (add.IsOpen)
        {
            await add.ContinueCommand.ExecuteAsync(null);
            if (add.IsOpen) visited.Add(add.Step);
        }

        visited.ShouldBe(AllSteps);
        host.Client.Sent.ShouldHaveSingleItem().ShouldBeOfType<ConnectAccount>();
    }

    [Fact]
    public async Task stay_on_authenticate_and_show_the_failure_when_sign_in_fails()
    {
        using var host = ProviderTestHost.Create(new FakeIpcClient([], available: false));
        var add = host.Providers.Add;
        add.BeginCommand.Execute(null);
        add.SelectCommand.Execute(add.Providers[0]);
        await add.ContinueCommand.ExecuteAsync(null);

        await add.ContinueCommand.ExecuteAsync(null);

        add.Step.ShouldBe(AddProviderStep.Authenticate);
        host.Providers.Error.ShouldNotBeNull().Message.ShouldContain("daemon");
    }

    [Fact]
    public async Task stay_on_the_sync_root_step_and_show_the_failure_when_the_job_cannot_be_created()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith());
        var add = host.Providers.Add;
        add.BeginCommand.Execute(null);
        add.SelectCommand.Execute(add.Providers[0]);
        while (add.Step != AddProviderStep.ChooseSyncRoot) await add.ContinueCommand.ExecuteAsync(null);
        add.IsChoosingSyncRoot.ShouldBeTrue();
        add.Selection.LocalRoot = "/mnt/sync";
        add.Selection.Select("docs", true);

        await add.ContinueCommand.ExecuteAsync(null);

        add.Step.ShouldBe(AddProviderStep.ChooseSyncRoot);
        add.Selection.StatusMessage.ShouldNotBeNull();
    }

    [Fact]
    public void close_and_forget_the_choice_when_cancelled()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith());
        var add = host.Providers.Add;
        add.BeginCommand.Execute(null);
        add.SelectCommand.Execute(add.Providers[0]);

        add.CancelCommand.Execute(null);

        add.IsOpen.ShouldBeFalse();
        add.SelectedProvider.ShouldBeNull();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith());
        var add = host.Providers.Add;
        add.BeginCommand.Execute(null);
        add.SelectCommand.Execute(add.Providers[0]);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => add.ContinueAsync(cancelled.Token));
    }
}

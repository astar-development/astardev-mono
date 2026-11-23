using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class SettingsHandlersShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task answer_get_settings_with_the_defaults()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<SettingsQuery>().HandleAsync(new GetSettings(), Token);

        var values = result.Value.ShouldBeOfType<Ok<SettingsView>>().Value.Values;
        values[SettingNames.Theme].ShouldBe("Dark");
        values[SettingNames.AuditRetentionDays].ShouldBe("90");
        values[SettingNames.TelemetryRetentionDays].ShouldBe("7");
    }

    [Fact]
    public async Task store_a_value_set_over_ipc()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var set = await host.Services.GetRequiredService<SetSettingHandler>().HandleAsync(new SetSetting(SettingNames.Theme, "Light"), Token);
        var read = await host.Services.GetRequiredService<SettingsQuery>().HandleAsync(new GetSettings(), Token);

        set.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        read.Value.ShouldBeOfType<Ok<SettingsView>>().Value.Values[SettingNames.Theme].ShouldBe("Light");
    }

    [Theory]
    [InlineData("colour", "red", "does not exist")]
    [InlineData(SettingNames.Theme, "Purple", "not allowed")]
    public async Task reject_a_setting_that_cannot_be_stored(string name, string value, string reason)
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<SetSettingHandler>().HandleAsync(new SetSetting(name, value), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain(reason);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SettingsQuery>().HandleAsync(new GetSettings(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SetSettingHandler>().HandleAsync(new SetSetting(SettingNames.Theme, "Light"), cancelled.Token));
    }
}

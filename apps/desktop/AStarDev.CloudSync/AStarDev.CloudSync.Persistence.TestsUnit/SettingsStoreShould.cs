using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class SettingsStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_the_default_for_a_setting_never_saved()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        var result = await host.Services.GetRequiredService<ISettingsStore>().GetAllAsync(Token);

        result.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value.ShouldBe(new Dictionary<string, string>
        {
            [SettingNames.Theme] = "Dark",
            [SettingNames.NotifyOnConflict] = "true",
            [SettingNames.NotifyOnError] = "true",
            [SettingNames.NotifyOnCompletion] = "false",
            [SettingNames.AuditRetentionDays] = "90",
            [SettingNames.TelemetryRetentionDays] = "7"
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task persist_a_saved_value_across_a_new_context()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ISettingsStore>();

        await store.SetAsync(SettingNames.AuditRetentionDays, "30", Token);
        await store.SetAsync(SettingNames.AuditRetentionDays, "45", Token);
        var result = await store.GetAllAsync(Token);

        var settings = result.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;
        settings[SettingNames.AuditRetentionDays].ShouldBe("45");
        settings[SettingNames.TelemetryRetentionDays].ShouldBe("7");
    }

    [Fact]
    public async Task return_a_failure_for_an_unknown_setting()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        var result = await host.Services.GetRequiredService<ISettingsStore>().SetAsync("colour", "red", Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new UnknownSetting("colour"));
    }

    [Theory]
    [InlineData(SettingNames.Theme, "Purple")]
    [InlineData(SettingNames.NotifyOnError, "yes")]
    [InlineData(SettingNames.AuditRetentionDays, "0")]
    [InlineData(SettingNames.AuditRetentionDays, "-3")]
    [InlineData(SettingNames.TelemetryRetentionDays, "seven")]
    [InlineData(SettingNames.TelemetryRetentionDays, "")]
    public async Task return_a_failure_for_a_value_the_setting_does_not_allow(string name, string value)
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ISettingsStore>();

        var result = await store.SetAsync(name, value, Token);
        var settings = await store.GetAllAsync(Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new InvalidSettingValue(name, value));
        settings.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value[name].ShouldNotBe(value);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ISettingsStore>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.GetAllAsync(cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.SetAsync(SettingNames.Theme, "Light", cancelled.Token));
    }
}

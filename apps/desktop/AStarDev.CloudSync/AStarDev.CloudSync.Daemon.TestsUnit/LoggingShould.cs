using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class LoggingShould : IDisposable
{
    private const string DisplayName = "Ada Lovelace";

    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-logging-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task not_log_account_display_names()
    {
        var logs = new TraceCapturingLoggerProvider();
        var provider = new StubCloudProvider
        {
            ConnectResult = new Ok<ProviderAccount>(new ProviderAccount("ada-id", DisplayName)),
            ReconnectResult = new Fail<ProviderError>(new AuthenticationFailed("expired"))
        };
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(provider).AddSingleton<ILoggerProvider>(logs));
        await host.InitialiseDatabaseAsync(Token);

        await host.Services.GetRequiredService<ConnectAccountHandler>().HandleAsync(new ConnectAccount(), Token);
        var stored = await host.Services.GetRequiredService<IAccountStore>().ListAsync(Token);
        var account = stored.Value.ShouldBeOfType<Ok<IReadOnlyList<Account>>>().Value.ShouldHaveSingleItem();
        await host.Services.GetServices<IStartupStage>().OfType<StateRecoveryStage>().Single().ExecuteAsync(Token);
        await host.Services.GetRequiredService<DisconnectAccountHandler>().HandleAsync(new DisconnectAccount(account.Id.Value), Token);

        logs.Entries.ShouldContain(entry => entry.Message.Contains("account", StringComparison.OrdinalIgnoreCase));
        logs.Entries.ShouldNotContain(entry => entry.Message.Contains(DisplayName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task log_through_the_generated_messages()
    {
        var logs = new EventCapturingLoggerProvider();
        var provider = new StubCloudProvider { ConnectResult = new Fail<ProviderError>(new AuthenticationFailed("expired")) };
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(provider).AddSingleton<ILoggerProvider>(logs));
        await host.InitialiseDatabaseAsync(Token);

        await host.Services.GetRequiredService<ConnectAccountHandler>().HandleAsync(new ConnectAccount(), Token);

        var entry = logs.Entries.Single(candidate => candidate.Category.EndsWith(nameof(ConnectAccountHandler), StringComparison.Ordinal));
        entry.EventId.ShouldBeInRange(6002, 6999);
    }

    [Fact]
    public async Task keep_personal_data_out_of_logs()
    {
        var logs = new EventCapturingLoggerProvider();
        var provider = new StubCloudProvider { ConnectResult = new Ok<ProviderAccount>(new ProviderAccount("ada-id", DisplayName)) };
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton<ICloudProvider>(provider).AddSingleton<ILoggerProvider>(logs));
        await host.InitialiseDatabaseAsync(Token);

        await host.Services.GetRequiredService<ConnectAccountHandler>().HandleAsync(new ConnectAccount(), Token);

        logs.Entries.ShouldContain(entry => entry.EventId >= 6002 && entry.EventId < 7000);
        logs.Entries.ShouldNotContain(entry => entry.Message.Contains(DisplayName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task not_log_the_full_database_path()
    {
        var logs = new TraceCapturingLoggerProvider();
        await using var host = DaemonTestHost.Create(configureServices: services => services.AddSingleton<ILoggerProvider>(logs));

        await host.InitialiseDatabaseAsync(Token);

        logs.Entries.ShouldContain(entry => entry.Message.Contains(Path.GetFileName(host.DatabasePath), StringComparison.Ordinal));
        logs.Entries.ShouldNotContain(entry => entry.Message.Contains(Path.GetDirectoryName(host.DatabasePath)!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task not_log_full_local_paths()
    {
        var logs = new TraceCapturingLoggerProvider();
        var missingRoot = Path.Combine(localRoot, "missing-root");
        await using var host = DaemonTestHost.Create(configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await host.AddJobAsync(account, missingRoot, Token);

        await host.Services.GetServices<IStartupStage>().OfType<BackgroundServicesStage>().Single().ExecuteAsync(Token);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!logs.Entries.Any(entry => entry.Message.Contains("Local observation", StringComparison.Ordinal)) && DateTimeOffset.UtcNow < deadline) await Task.Delay(20, Token);

        logs.Entries.ShouldContain(entry => entry.Message.Contains("Local observation", StringComparison.Ordinal));
        logs.Entries.ShouldNotContain(entry => entry.Message.Contains(localRoot, StringComparison.Ordinal));
    }
}

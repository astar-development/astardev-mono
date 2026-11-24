using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class DatabaseInitialiserShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task log_through_the_generated_messages()
    {
        var logs = new EventCapturingLoggerProvider();
        await using var host = PersistenceTestHost.Create(configureServices: services => services.AddSingleton<ILoggerProvider>(logs));

        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        logs.Entries.ShouldContain(entry => entry.Category.EndsWith(nameof(DatabaseInitialiser), StringComparison.Ordinal) && entry.EventId == 4003);
    }

    [Fact]
    public async Task log_a_corrupt_database_through_the_generated_messages()
    {
        var logs = new EventCapturingLoggerProvider();
        await using var host = PersistenceTestHost.Create(configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        Directory.CreateDirectory(Path.GetDirectoryName(host.DatabasePath)!);
        await File.WriteAllTextAsync(host.DatabasePath, new string('x', 4096), Token);

        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        logs.Entries.ShouldContain(entry => entry.EventId == 4004);
    }

    [Fact]
    public async Task switch_on_write_ahead_logging()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        await using var connection = new SqliteConnection($"Data Source={host.DatabasePath}");
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";

        (await command.ExecuteScalarAsync(Token)).ShouldBe("wal");
    }
}

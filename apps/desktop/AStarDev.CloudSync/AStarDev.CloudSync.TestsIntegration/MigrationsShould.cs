using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class MigrationsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task create_the_schema_on_an_empty_database()
    {
        await using var host = DatabaseTestHost.Create();

        var result = await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var tables = await TableNamesAsync(host.DatabasePath);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Normal);
        tables.ShouldBe(["Accounts", "Categories", "ClassificationState", "Conflicts", "CrawlCheckpoints", "DeltaStates", "EventJournal", "FileLinks", "ItemSnapshots", "Items", "KnownSnapshots", "Operations", "ProviderState", "Settings", "StagedKnownSnapshots", "SyncJobs", "SyncScopes", "Terms"]);
    }

    [Fact]
    public async Task leave_the_schema_and_data_alone_when_applied_twice()
    {
        await using var host = DatabaseTestHost.Create();
        var initialiser = host.Services.GetRequiredService<IDatabaseInitialiser>();
        await initialiser.InitialiseAsync(Token);

        var second = await initialiser.InitialiseAsync(Token);

        second.Value.ShouldBeOfType<Ok<Unit>>();
        (await TableNamesAsync(host.DatabasePath)).Count.ShouldBe(18);
    }

    [Fact]
    public async Task apply_the_settings_migration_to_an_existing_database()
    {
        await using var host = DatabaseTestHost.Create();
        var initialiser = host.Services.GetRequiredService<IDatabaseInitialiser>();
        await initialiser.InitialiseAsync(Token);
        await ExecuteAsync(host.DatabasePath, "DROP TABLE Settings; DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddSettings';");
        (await TableNamesAsync(host.DatabasePath)).ShouldNotContain("Settings");

        var result = await initialiser.InitialiseAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        (await TableNamesAsync(host.DatabasePath)).ShouldContain("Settings");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DatabaseTestHost.Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(cancelled.Token));
    }

    private static async Task ExecuteAsync(string databasePath, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Token);
    }

    private static async Task<List<string>> TableNamesAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE '\\_\\_%' ESCAPE '\\' AND name NOT LIKE 'sqlite\\_%' ESCAPE '\\' ORDER BY name";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Token);
        while (await reader.ReadAsync(Token)) names.Add(reader.GetString(0));

        return names;
    }
}

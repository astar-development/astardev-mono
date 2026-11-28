using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class StoreFullTicksShould
{
    private const string PreviousMigration = "20261128145537_AddEventJournalTypeIndex";

    private static readonly DateTimeOffset East = new DateTimeOffset(2026, 11, 28, 10, 0, 0, TimeSpan.FromHours(5.5)).AddTicks(123_456);

    private static readonly DateTimeOffset West = new DateTimeOffset(2026, 11, 28, 10, 0, 0, TimeSpan.FromHours(-8)).AddTicks(654_321);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task read_back_timestamps_written_in_the_binary_format_as_the_same_instant()
    {
        await using var host = PersistenceTestHost.Create();
        var operation = await AddOperationInBinaryFormatAsync(host);

        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var read = (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.ShouldHaveSingleItem();

        read.Id.ShouldBe(operation.Id);
        read.CreatedUtc.UtcTicks.ShouldBe(TenthOfAMillisecond(East));
        read.NextAttemptUtc.UtcTicks.ShouldBe(TenthOfAMillisecond(West));
        read.CreatedUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task restore_the_binary_format_when_migrated_down()
    {
        await using var host = PersistenceTestHost.Create();
        await AddOperationInBinaryFormatAsync(host);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        await MigrateToAsync(host, PreviousMigration);
        var stored = await StoredAsync(host);

        stored.ShouldBe((Binary(East.ToUniversalTime()), Binary(West.ToUniversalTime())));
    }

    private static async Task<Operation> AddOperationInBinaryFormatAsync(PersistenceTestHost host)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(host.DatabasePath)!);
        await MigrateToAsync(host, PreviousMigration);
        var job = (await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge)).Id;
        var operation = new Operation(new OperationId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, OperationStatus.Pending, 0, West, null, East);
        (await host.Services.GetRequiredService<IOperationStore>().EnqueueAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        await context.Database.ExecuteSqlAsync($"UPDATE Operations SET CreatedUtc = {Binary(East)}, NextAttemptUtc = {Binary(West)}", Token);

        return operation;
    }

    private static async Task MigrateToAsync(PersistenceTestHost host, string migration)
    {
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        await context.GetService<IMigrator>().MigrateAsync(migration, Token);
    }

    private static async Task<(long CreatedUtc, long NextAttemptUtc)> StoredAsync(PersistenceTestHost host)
    {
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        var created = await context.Database.SqlQuery<long>($"SELECT CreatedUtc AS Value FROM Operations").SingleAsync(Token);
        var nextAttempt = await context.Database.SqlQuery<long>($"SELECT NextAttemptUtc AS Value FROM Operations").SingleAsync(Token);

        return (created, nextAttempt);
    }

    private static long Binary(DateTimeOffset value) => (long)new DateTimeOffsetToBinaryConverter().ConvertToProvider(value)!;

    private static long TenthOfAMillisecond(DateTimeOffset value) => value.UtcTicks / 1000 * 1000;
}

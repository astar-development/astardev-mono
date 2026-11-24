using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Persistence.TestsUnit.ClassificationTestData;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class ClassificationStateStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task report_no_seed_version_before_one_is_recorded()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        Ok(await host.Services.GetRequiredService<IClassificationStateStore>().GetSeedVersionAsync(Token)).Value.ShouldBeOfType<None<string>>();
    }

    [Fact]
    public async Task remember_the_latest_seed_version()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IClassificationStateStore>();

        await store.SetSeedVersionAsync("1", Token);
        await store.SetSeedVersionAsync("2", Token);

        Ok(await store.GetSeedVersionAsync(Token)).Value.ShouldBeOfType<Some<string>>().Value.ShouldBe("2");
    }
}

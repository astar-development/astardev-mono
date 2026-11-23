using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class ProviderStateStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_nothing_before_any_sync()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        var result = await host.Services.GetRequiredService<IProviderStateStore>().ListLastSyncAsync(Token);

        result.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<SyncJobId, DateTimeOffset>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IProviderStateStore>().ListLastSyncAsync(cancelled.Token));
    }
}

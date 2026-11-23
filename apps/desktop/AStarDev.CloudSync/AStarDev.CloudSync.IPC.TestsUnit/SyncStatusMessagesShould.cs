namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class SyncStatusMessagesShould
{
    [Theory]
    [InlineData(ProviderHealthState.Healthy, null)]
    [InlineData(ProviderHealthState.Degraded, 1_700_000_000L)]
    [InlineData(ProviderHealthState.Unhealthy, 1_800_000_000L)]
    public void round_trip_a_status_view(ProviderHealthState health, long? lastActivitySeconds)
    {
        var view = new SyncStatusView(health, 12, 3, lastActivitySeconds is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null);

        var element = IpcJson.ToElement(new SyncStatusChanged(view)).ShouldSucceed();

        IpcJson.FromElement<SyncStatusChanged>(element).ShouldSucceed().Status.ShouldBe(view);
    }
}

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class DashboardMessagesShould
{
    [Fact]
    public void round_trip_a_dashboard_view()
    {
        var view = new DashboardView(ProviderHealthState.Degraded, DashboardStage.AnalyseChanges, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), null, null, 40, 100, 8.5, TimeSpan.FromSeconds(7), [new DashboardProviderView("OneDrive", ProviderHealthState.Healthy)], 1, 2, 3, 4, 5, 6, 7, [new DashboardOperationView("UploadFile", "a.txt")], ["QueueDrained"]);

        var element = IpcJson.ToElement(new DashboardChanged(view)).ShouldSucceed();

        IpcJson.ToElement(IpcJson.FromElement<DashboardChanged>(element).ShouldSucceed()).ShouldSucceed().GetRawText().ShouldBe(element.GetRawText());
    }
}

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class CrawlProgressMessagesShould
{
    private static readonly Guid Job = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void round_trip_the_extended_progress_event()
    {
        var sent = new CrawlProgressChanged(Job, "docs", 400, false, CrawlPhase.Crawling, 1000, 80.5, TimeSpan.FromSeconds(7.5));

        var element = IpcJson.ToElement(sent).ShouldSucceed();

        IpcJson.FromElement<CrawlProgressChanged>(element).ShouldSucceed().ShouldBe(sent);
    }

    [Fact]
    public void carry_no_estimate_when_the_total_is_unknown()
    {
        var sent = new CrawlProgressChanged(Job, "docs", 400, false, CrawlPhase.Syncing);

        var element = IpcJson.ToElement(sent).ShouldSucceed();
        var received = IpcJson.FromElement<CrawlProgressChanged>(element).ShouldSucceed();

        received.ItemsTotal.ShouldBeNull();
        received.EstimatedRemaining.ShouldBeNull();
        element.GetProperty("phase").GetString().ShouldBe("Syncing");
    }
}

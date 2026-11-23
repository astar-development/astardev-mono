namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class CrawlProgressMessagesShould
{
    private static readonly Guid Job = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void round_trip_the_progress_event()
    {
        var sent = new CrawlProgressChanged(Job, "docs", 400, false, CrawlPhase.Crawling, 1000);

        var element = IpcJson.ToElement(sent).ShouldSucceed();

        IpcJson.FromElement<CrawlProgressChanged>(element).ShouldSucceed().ShouldBe(sent);
    }

    [Fact]
    public void carry_no_total_when_the_provider_did_not_say()
    {
        var sent = new CrawlProgressChanged(Job, "docs", 400, false, CrawlPhase.Syncing);

        var element = IpcJson.ToElement(sent).ShouldSucceed();
        var received = IpcJson.FromElement<CrawlProgressChanged>(element).ShouldSucceed();

        received.ItemsTotal.ShouldBeNull();
        element.GetProperty("phase").GetString().ShouldBe("Syncing");
    }
}

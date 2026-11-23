namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class HistoryMessagesShould
{
    private static readonly DateTimeOffset Stamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    [Fact]
    public void round_trip_a_query_with_every_filter()
    {
        var query = new GetHistory("OneDrive", "UploadFile", "Failed", Stamp, Stamp.AddDays(1), "photos");

        var element = IpcJson.ToElement(query).ShouldSucceed();

        IpcJson.FromElement<GetHistory>(element).ShouldSucceed().ShouldBe(query);
    }

    [Fact]
    public void round_trip_a_query_with_no_filter()
    {
        var element = IpcJson.ToElement(new GetHistory()).ShouldSucceed();

        IpcJson.FromElement<GetHistory>(element).ShouldSucceed().ShouldBe(new GetHistory());
    }

    [Fact]
    public void round_trip_a_history_view()
    {
        var view = new HistoryView([new HistoryEntryView(Guid.NewGuid(), "OneDrive", "UploadFile", "Completed", Stamp, "a/b.txt", 12)]);

        var element = IpcJson.ToElement(view).ShouldSucceed();

        IpcJson.FromElement<HistoryView>(element).ShouldSucceed().Entries.ShouldBe(view.Entries);
    }
}

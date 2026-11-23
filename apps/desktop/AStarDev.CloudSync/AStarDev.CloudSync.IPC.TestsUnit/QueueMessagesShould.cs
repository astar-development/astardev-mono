namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class QueueMessagesShould
{
    private static readonly DateTimeOffset Stamp = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    private static QueueView View() => new(2, 1, 4, 3, [new QueuedOperationView(Guid.NewGuid(), "OneDrive", "UploadFile", "Pending", "a/b.txt", 12, Stamp)]);

    [Fact]
    public void round_trip_a_query()
    {
        var element = IpcJson.ToElement(new GetQueue()).ShouldSucceed();

        IpcJson.FromElement<GetQueue>(element).ShouldSucceed().ShouldBe(new GetQueue());
    }

    [Fact]
    public void round_trip_a_queue_view()
    {
        var view = View();

        var element = IpcJson.ToElement(view).ShouldSucceed();

        var back = IpcJson.FromElement<QueueView>(element).ShouldSucceed();
        (back.Pending, back.Running, back.Completed, back.Failed).ShouldBe((2, 1, 4, 3));
        back.Operations.ShouldBe(view.Operations);
    }

    [Fact]
    public void round_trip_a_queue_changed_event()
    {
        var changed = new QueueChanged(View());

        var element = IpcJson.ToElement(changed).ShouldSucceed();

        IpcJson.FromElement<QueueChanged>(element).ShouldSucceed().Queue.Operations.ShouldBe(changed.Queue.Operations);
    }
}

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class FailureReplyShould
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    [Fact]
    public void round_trip_the_trace_id()
    {
        var sent = new CommandResult(CommandOutcome.Rejected, "offline", TraceId);

        var element = IpcJson.ToElement(sent).ShouldSucceed();

        IpcJson.FromElement<CommandResult>(element).ShouldSucceed().ShouldBe(sent);
    }

    [Fact]
    public void carry_no_trace_id_by_default()
    {
        var element = IpcJson.ToElement(new CommandResult(CommandOutcome.Accepted)).ShouldSucceed();

        IpcJson.FromElement<CommandResult>(element).ShouldSucceed().TraceId.ShouldBeNull();
    }
}

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
    public void round_trip_the_error_code()
    {
        var sent = new ErrorEnvelope(7, "offline", "CS-IPC-0003");

        var received = IpcJson.Decode(IpcJson.Encode(sent).ShouldSucceed()).ShouldSucceed();

        received.ShouldBe(sent);
    }

    [Fact]
    public void read_a_valid_code_from_the_peer()
        => IpcErrorCodes.Read("CS-IPC-0003").Value.ShouldBe("CS-IPC-0003");

    [Fact]
    public void drop_a_code_that_is_not_readable()
        => IpcErrorCodes.Read("nonsense").Value.ShouldBeNullOrEmpty();

    [Fact]
    public void carry_no_trace_id_by_default()
    {
        var element = IpcJson.ToElement(new CommandResult(CommandOutcome.Accepted)).ShouldSucceed();

        IpcJson.FromElement<CommandResult>(element).ShouldSucceed().TraceId.ShouldBeNull();
    }
}

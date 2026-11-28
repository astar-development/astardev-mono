using System.Text;

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class EnvelopesShould
{
    private static readonly Stamped Sample = new("notes.txt", new DateTimeOffset(2026, 11, 28, 11, 24, 33, 960, TimeSpan.Zero));

    private interface IStampedSummary
    {
        string Name { get; }
    }

    private sealed record Stamped(string Name, DateTimeOffset At) : IStampedSummary;

    [Fact]
    public void set_the_help_link_on_a_malformed_envelope()
    {
        var error = IpcJson.Decode("null"u8.ToArray()).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();

        error.HelpLink.ShouldBe("https://astar-development.co.uk/errors/CS-IPC-0005");
    }

    [Fact]
    public void round_trip_a_command_with_its_payload()
        => IpcJson.FromElement<Stamped>(RoundTrip(new CommandEnvelope(3, nameof(Stamped), Sample)).ShouldBeOfType<CommandEnvelope>().Payload).ShouldSucceed().ShouldBe(Sample);

    [Fact]
    public void round_trip_a_query_with_its_payload()
        => IpcJson.FromElement<Stamped>(RoundTrip(new QueryEnvelope(4, nameof(Stamped), Sample)).ShouldBeOfType<QueryEnvelope>().Payload).ShouldSucceed().ShouldBe(Sample);

    [Fact]
    public void round_trip_a_response_with_its_payload()
        => IpcJson.FromElement<Stamped>(RoundTrip(new ResponseEnvelope(5, Sample)).ShouldBeOfType<ResponseEnvelope>().Payload).ShouldSucceed().ShouldBe(Sample);

    [Fact]
    public void round_trip_an_event_with_its_payload()
        => IpcJson.FromElement<Stamped>(RoundTrip(new EventEnvelope(nameof(Stamped), Sample)).ShouldBeOfType<EventEnvelope>().Payload).ShouldSucceed().ShouldBe(Sample);

    [Fact]
    public void round_trip_an_error()
        => RoundTrip(new ErrorEnvelope(6, "offline", "CS-IPC-0003")).ShouldBe(new ErrorEnvelope(6, "offline", "CS-IPC-0003"));

    [Fact]
    public void write_the_kind_discriminator_and_the_payload_in_one_pass()
        => Encoding.UTF8.GetString(IpcJson.Encode(new ResponseEnvelope(5, Sample)).ShouldSucceed()).ShouldBe("""{"kind":"response","id":5,"payload":{"name":"notes.txt","at":"2026-11-28T11:24:33.96+00:00"}}""");

    [Fact]
    public void keep_every_runtime_property_of_a_payload_held_as_an_interface()
    {
        IStampedSummary summary = Sample;

        IpcJson.FromElement<Stamped>(RoundTrip(new ResponseEnvelope(5, summary)).ShouldBeOfType<ResponseEnvelope>().Payload).ShouldSucceed().ShouldBe(Sample);
    }

    [Fact]
    public void decode_a_frame_written_by_the_earlier_two_pass_encoder()
    {
        var frame = """{"kind":"response","id":5,"payload":{"name":"notes.txt","at":"2026-11-28T11:24:33.96+00:00"}}"""u8.ToArray();

        var response = IpcJson.Decode(frame).ShouldSucceed().ShouldBeOfType<ResponseEnvelope>();

        IpcJson.FromElement<Stamped>(response.Payload).ShouldSucceed().ShouldBe(Sample);
    }

    [Fact]
    public void return_a_malformed_frame_for_a_payload_that_is_json_null()
    {
        var response = IpcJson.Decode("""{"kind":"response","id":5,"payload":null}"""u8.ToArray()).ShouldSucceed().ShouldBeOfType<ResponseEnvelope>();

        IpcJson.FromElement<Stamped>(response.Payload).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();
    }

    [Fact]
    public void return_a_malformed_frame_for_an_envelope_without_its_payload()
        => IpcJson.Decode("""{"kind":"response","id":5}"""u8.ToArray()).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();

    private static Envelope RoundTrip(Envelope sent) => IpcJson.Decode(IpcJson.Encode(sent).ShouldSucceed()).ShouldSucceed();
}

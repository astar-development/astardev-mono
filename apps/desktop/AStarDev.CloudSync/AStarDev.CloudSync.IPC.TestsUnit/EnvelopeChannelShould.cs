using System.Text;

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class EnvelopeChannelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_a_failure_for_a_frame_that_is_not_an_envelope()
    {
        using var stream = new MemoryStream();
        (await FrameCodec.WriteAsync(stream, Encoding.UTF8.GetBytes("this is not json"), Token)).ShouldSucceed();
        stream.Position = 0;
        await using var channel = new EnvelopeChannel(stream);

        (await channel.ReceiveAsync(Token)).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();
    }

    [Fact]
    public async Task report_a_peer_that_closed_the_connection_as_a_lost_connection()
    {
        using var stream = new MemoryStream();
        await using var channel = new EnvelopeChannel(stream);

        (await channel.ReceiveAsync(Token)).ShouldFail().Value.ShouldBeOfType<ConnectionLost>();
    }

    [Fact]
    public async Task round_trip_an_envelope()
    {
        using var stream = new MemoryStream();
        await using var channel = new EnvelopeChannel(stream);
        var sent = new SubscribeEnvelope(7);

        (await channel.SendAsync(sent, Token)).ShouldSucceed();
        stream.Position = 0;

        (await channel.ReceiveAsync(Token)).ShouldSucceed().ShouldBe(sent);
    }

    [Fact]
    public async Task refuse_an_envelope_whose_payload_makes_the_frame_larger_than_the_maximum()
    {
        using var stream = new MemoryStream();
        await using var channel = new EnvelopeChannel(stream);

        var failure = (await channel.SendAsync(new ResponseEnvelope(7, new string('x', FrameCodec.MaxFrameSize)), Token)).ShouldFail();

        failure.Value.ShouldBeOfType<FrameTooLarge>().MaxSize.ShouldBe(FrameCodec.MaxFrameSize);
        stream.Length.ShouldBe(0);
    }
}

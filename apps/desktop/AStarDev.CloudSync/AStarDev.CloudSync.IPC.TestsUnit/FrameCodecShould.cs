using System.Buffers.Binary;

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class FrameCodecShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Header(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);
        return header;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4096)]
    [InlineData(200_000)]
    [InlineData(FrameCodec.MaxFrameSize)]
    public async Task round_trip_a_message_of_any_size(int size)
    {
        var message = new byte[size];
        new Random(size).NextBytes(message);
        using var stream = new MemoryStream();

        (await FrameCodec.WriteAsync(stream, message, Token)).ShouldSucceed();
        stream.Position = 0;
        var read = (await FrameCodec.ReadAsync(stream, Token)).ShouldSucceed();

        read.Value.ShouldBeOfType<Frame>().Payload.ShouldBe(message);
    }

    [Fact]
    public async Task reject_a_frame_over_the_maximum_size()
    {
        using var stream = new MemoryStream();

        var failure = (await FrameCodec.WriteAsync(stream, new byte[FrameCodec.MaxFrameSize + 1], Token)).ShouldFail();

        failure.Value.ShouldBeOfType<FrameTooLarge>().Size.ShouldBe(FrameCodec.MaxFrameSize + 1);
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public async Task return_a_failure_for_an_oversize_frame()
    {
        // a peer announcing an oversized frame is refused from the header alone, before any payload is read
        using var stream = new MemoryStream(Header(FrameCodec.MaxFrameSize + 1));

        var failure = (await FrameCodec.ReadAsync(stream, Token)).ShouldFail();

        failure.Value.ShouldBeOfType<FrameTooLarge>().Size.ShouldBe(FrameCodec.MaxFrameSize + 1);
    }

    [Fact]
    public async Task return_a_failure_for_a_negative_frame_length()
    {
        using var stream = new MemoryStream(Header(-1));

        var failure = (await FrameCodec.ReadAsync(stream, Token)).ShouldFail();

        failure.Value.ShouldBeOfType<MalformedFrame>();
    }

    [Fact]
    public async Task return_a_failure_for_a_cut_short_frame()
    {
        using var stream = new MemoryStream();
        (await FrameCodec.WriteAsync(stream, new byte[10], Token)).ShouldSucceed();

        // payload cut short
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;
        (await FrameCodec.ReadAsync(stream, Token)).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();

        // header cut short
        using var halfHeader = new MemoryStream(Header(10)[..2]);
        (await FrameCodec.ReadAsync(halfHeader, Token)).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();
    }

    [Fact]
    public async Task read_consecutive_frames_and_report_the_end_of_the_stream_as_a_success()
    {
        using var stream = new MemoryStream();
        (await FrameCodec.WriteAsync(stream, new byte[] { 1, 2 }, Token)).ShouldSucceed();
        (await FrameCodec.WriteAsync(stream, new byte[] { 3 }, Token)).ShouldSucceed();
        stream.Position = 0;

        (await FrameCodec.ReadAsync(stream, Token)).ShouldSucceed().Value.ShouldBeOfType<Frame>().Payload.ShouldBe(new byte[] { 1, 2 });
        (await FrameCodec.ReadAsync(stream, Token)).ShouldSucceed().Value.ShouldBeOfType<Frame>().Payload.ShouldBe(new byte[] { 3 });
        (await FrameCodec.ReadAsync(stream, Token)).ShouldSucceed().Value.ShouldBeOfType<EndOfStream>();
    }

    [Fact]
    public async Task return_a_failure_when_the_stream_is_closed()
    {
        var stream = new MemoryStream();
        await stream.DisposeAsync();

        (await FrameCodec.WriteAsync(stream, new byte[1], Token)).ShouldFail().Value.ShouldBeOfType<ConnectionLost>();
        (await FrameCodec.ReadAsync(stream, Token)).ShouldFail().Value.ShouldBeOfType<ConnectionLost>();
    }

    [Fact]
    public async Task honour_an_already_cancelled_token()
    {
        using var stream = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await ResultAssertions.ShouldBeCancelledAsync(async () => await FrameCodec.WriteAsync(stream, new byte[1], cancelled.Token));
        await ResultAssertions.ShouldBeCancelledAsync(async () => await FrameCodec.ReadAsync(stream, cancelled.Token));
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public async Task honour_cancellation_while_waiting_for_a_frame()
    {
        await using var stream = new SilentStream();
        using var cancelling = new CancellationTokenSource();

        var pending = FrameCodec.ReadAsync(stream, cancelling.Token);
        await cancelling.CancelAsync();

        await ResultAssertions.ShouldBeCancelledAsync(async () => await pending);
    }

    /// <summary>A peer that never says anything: every read waits until it is cancelled.</summary>
    private sealed class SilentStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}

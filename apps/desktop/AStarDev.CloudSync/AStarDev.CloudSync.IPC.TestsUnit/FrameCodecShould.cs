using System.Buffers.Binary;

namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class FrameCodecShould
{
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

        await FrameCodec.WriteAsync(stream, message, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var received = await FrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken);

        received.ShouldBe(message);
    }

    [Fact]
    public async Task reject_a_frame_over_the_maximum_size()
    {
        using var stream = new MemoryStream();

        await Should.ThrowAsync<InvalidDataException>(async () =>
            await FrameCodec.WriteAsync(stream, new byte[FrameCodec.MaxFrameSize + 1], TestContext.Current.CancellationToken));
        stream.Length.ShouldBe(0);

        // a peer announcing an oversized frame is refused from the header alone, before any payload is read
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, FrameCodec.MaxFrameSize + 1);
        using var oversized = new MemoryStream(header);
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await FrameCodec.ReadAsync(oversized, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task read_consecutive_frames_and_signal_the_end_of_the_stream()
    {
        using var stream = new MemoryStream();
        await FrameCodec.WriteAsync(stream, new byte[] { 1, 2 }, TestContext.Current.CancellationToken);
        await FrameCodec.WriteAsync(stream, new byte[] { 3 }, TestContext.Current.CancellationToken);
        stream.Position = 0;

        (await FrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken)).ShouldBe(new byte[] { 1, 2 });
        (await FrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken)).ShouldBe(new byte[] { 3 });
        (await FrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task reject_a_frame_cut_short()
    {
        using var stream = new MemoryStream();
        await FrameCodec.WriteAsync(stream, new byte[10], TestContext.Current.CancellationToken);
        stream.SetLength(stream.Length - 1);
        stream.Position = 0;

        await Should.ThrowAsync<EndOfStreamException>(async () =>
            await FrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken));
    }
}

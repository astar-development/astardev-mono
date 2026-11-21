using System.Buffers.Binary;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>One complete frame payload, with the length header already removed.</summary>
internal sealed record Frame(byte[] Payload);

/// <summary>The stream ended cleanly between frames. Expected, so a success case, not a failure.</summary>
internal sealed record EndOfStream;

/// <summary>What one read produced: a whole <see cref="Frame"/>, or a clean <see cref="EndOfStream"/>.</summary>
internal union FrameRead(Frame, EndOfStream);

/// <summary>Length-prefixed framing: a 4 byte big-endian payload length, then the payload.</summary>
internal static class FrameCodec
{
    private const int HeaderSize = 4;

    /// <summary>Bounds what a peer can make us allocate.</summary>
    public const int MaxFrameSize = 1024 * 1024;

    /// <summary>Writes the header and <paramref name="payload"/> in a single write, then flushes. An oversized payload is refused before any byte is written.</summary>
    /// <returns><see cref="FrameTooLarge"/> when <paramref name="payload"/> exceeds <see cref="MaxFrameSize"/>, or <see cref="ConnectionLost"/> when the stream fails.</returns>
    public static async Task<Result<Unit, IpcError>> WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (payload.Length > MaxFrameSize)
        {
            return new Fail<IpcError>(new FrameTooLarge(payload.Length, MaxFrameSize));
        }

        // one buffer, one write: the frame is never split on the stream
        var frame = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
        payload.CopyTo(frame.AsMemory(HeaderSize));
        var written = await Try.RunAsync(async token =>
        {
            await stream.WriteAsync(frame, token);
            await stream.FlushAsync(token);
            return default(Unit);
        }, cancellationToken);

        return written.ToResult(IpcErrors.AsConnectionLost);
    }

    /// <summary>
    /// Reads exactly one frame. The declared length is checked before the payload buffer is allocated.
    /// A stream that ends before the first header byte is <see cref="EndOfStream"/>; one that ends inside a frame is <see cref="MalformedFrame"/>.
    /// </summary>
    public static async Task<Result<FrameRead, IpcError>> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        var headerBytes = await Try.RunAsync(token => ReadUpToAsync(stream, header, token), cancellationToken);

        return headerBytes.ToResult(IpcErrors.AsConnectionLost) switch
        {
            Ok<int> { Value: 0 } => new Ok<FrameRead>(new EndOfStream()),
            Ok<int> { Value: < HeaderSize } => new Fail<IpcError>(new MalformedFrame("The stream ended inside a frame header.")),
            Ok<int> => await ReadPayloadAsync(stream, BinaryPrimitives.ReadInt32BigEndian(header), cancellationToken),
            Fail<IpcError> fail => fail
        };
    }

    private static async Task<Result<FrameRead, IpcError>> ReadPayloadAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        // refused from the header alone, before any payload is allocated or read
        if (length < 0) return new Fail<IpcError>(new MalformedFrame($"A frame cannot have a length of {length}."));

        if (length > MaxFrameSize) return new Fail<IpcError>(new FrameTooLarge(length, MaxFrameSize));

        var payload = new byte[length];
        var payloadBytes = await Try.RunAsync(token => ReadUpToAsync(stream, payload, token), cancellationToken);

        return payloadBytes.ToResult(IpcErrors.AsConnectionLost) switch
        {
            Ok<int> { Value: var read } when read < length => new Fail<IpcError>(new MalformedFrame("The stream ended inside a frame payload.")),
            Ok<int> => new Ok<FrameRead>(new Frame(payload)),
            Fail<IpcError> fail => fail
        };
    }

    /// <returns>How many bytes were read: less than the buffer only when the stream ended.</returns>
    private static async Task<int> ReadUpToAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0) break;

            read += count;
        }

        return read;
    }
}

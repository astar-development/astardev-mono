using System.Buffers.Binary;

namespace AStarDev.CloudSync.IPC;

/// <summary>Length-prefixed framing: a 4 byte big-endian payload length, then the payload.</summary>
internal static class FrameCodec
{
    private const int HeaderSize = 4;

    /// <summary>Bounds what a peer can make us allocate.</summary>
    public const int MaxFrameSize = 1024 * 1024;

    public static async ValueTask WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (payload.Length > MaxFrameSize)
        {
            throw new InvalidDataException($"Frame of {payload.Length} bytes exceeds the maximum of {MaxFrameSize}.");
        }

        // one buffer, one write: the frame is never split on the stream
        var frame = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
        payload.CopyTo(frame.AsMemory(HeaderSize));
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <returns>The payload, or null when the stream ended cleanly between frames.</returns>
    public static async ValueTask<byte[]?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        if (!await ReadExactlyOrEndAsync(stream, header, cancellationToken))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is < 0 or > MaxFrameSize)
        {
            throw new InvalidDataException($"Frame of {length} bytes exceeds the maximum of {MaxFrameSize}.");
        }

        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return payload;
    }

    private static async ValueTask<bool> ReadExactlyOrEndAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                return read == 0 ? false : throw new EndOfStreamException("The stream ended inside a frame header.");
            }

            read += count;
        }

        return true;
    }
}

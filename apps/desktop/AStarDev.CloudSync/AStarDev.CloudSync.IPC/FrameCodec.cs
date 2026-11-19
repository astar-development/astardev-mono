using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

internal sealed record Frame(byte[] Payload);

/// <summary>The stream ended cleanly between frames. Expected, so a success case, not a failure.</summary>
internal sealed record EndOfStream;

internal union FrameRead(Frame, EndOfStream);

internal static class FrameCodec
{
    public const int MaxFrameSize = 1024 * 1024;

    public static Task<Result<Unit, IpcError>> WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public static Task<Result<FrameRead, IpcError>> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

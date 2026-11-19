namespace AStarDev.CloudSync.IPC;

internal static class FrameCodec
{
    public const int MaxFrameSize = 1024 * 1024;

    public static ValueTask WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public static ValueTask<byte[]?> ReadAsync(Stream stream, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Sends and receives envelopes over one connection. Safe for concurrent senders.</summary>
internal sealed class EnvelopeChannel(Stream stream) : IAsyncDisposable
{
    private readonly SemaphoreSlim sendLock = new(1, 1);

    public async Task<Result<Unit, IpcError>> SendAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return IpcJson.Encode(envelope) switch
        {
            Ok<byte[]> encoded => await WriteExclusivelyAsync(encoded.Value, cancellationToken),
            Fail<IpcError> fail => fail
        };
    }

    /// <summary>A peer that closed the connection is reported as <see cref="ConnectionLost"/>.</summary>
    public async Task<Result<Envelope, IpcError>> ReceiveAsync(CancellationToken cancellationToken)
        => await FrameCodec.ReadAsync(stream, cancellationToken) switch
        {
            Ok<FrameRead> { Value: Frame frame } => IpcJson.Decode(frame.Payload),
            Ok<FrameRead> => new Fail<IpcError>(new ConnectionLost("The peer closed the connection.")),
            Fail<IpcError> fail => fail
        };

    public ValueTask DisposeAsync() => stream.DisposeAsync();

    private async Task<Result<Unit, IpcError>> WriteExclusivelyAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            return await FrameCodec.WriteAsync(stream, bytes, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }
}

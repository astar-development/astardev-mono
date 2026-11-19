using System.Text.Json;

namespace AStarDev.CloudSync.IPC;

/// <summary>Sends and receives envelopes over one connection. Safe for concurrent senders.</summary>
internal sealed class EnvelopeChannel(Stream stream) : IAsyncDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public async Task SendAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, IpcJson.Options);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await FrameCodec.WriteAsync(stream, bytes, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <returns>The next envelope, or null when the peer closed the connection.</returns>
    public async Task<Envelope?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var bytes = await FrameCodec.ReadAsync(stream, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<Envelope>(bytes, IpcJson.Options);
    }

    public ValueTask DisposeAsync() => stream.DisposeAsync();
}

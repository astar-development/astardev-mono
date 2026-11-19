using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>Sends and receives envelopes over one connection. Safe for concurrent senders.</summary>
internal sealed class EnvelopeChannel(Stream stream) : IAsyncDisposable
{
    public Task<Result<Unit, IpcError>> SendAsync(Envelope envelope, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <summary>A peer that closed the connection is reported as <see cref="ConnectionLost"/>.</summary>
    public Task<Result<Envelope, IpcError>> ReceiveAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public ValueTask DisposeAsync() => stream.DisposeAsync();
}

using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;

namespace AStarDev.CloudSync.UI;

/// <summary>Keeps the running instance's lock and hand-off listener alive until the host is disposed.</summary>
internal sealed class InstanceHold : IAsyncDisposable
{
    private Option<PrimaryInstance> held = Option.None<PrimaryInstance>();

    /// <summary>Takes ownership of the running instance; it is disposed with this hold.</summary>
    /// <param name="primary">The running instance.</param>
    public void Keep(PrimaryInstance primary) => held = Option.Some(primary);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => held.Match(primary => primary.DisposeAsync(), () => ValueTask.CompletedTask);
}

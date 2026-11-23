using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class FailingTelemetryStore : ITelemetryStore
{
    private static readonly Fail<StoreError> Failure = new(new StorageFailed("the telemetry database is unavailable"));

    public Task<Result<Unit, StoreError>> AppendAsync(IReadOnlyList<TelemetryEntry> entries, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Unit, StoreError>>(Failure);
    }

    public Task<Result<IReadOnlyList<TelemetryEntry>, StoreError>> QueryAsync(TelemetryQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<IReadOnlyList<TelemetryEntry>, StoreError>>(Failure);
    }

    public Task<Result<int, StoreError>> PruneAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<int, StoreError>>(Failure);
    }
}

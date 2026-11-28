using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Keeps the last successful read of the state database and hands it back while <see cref="IDatabaseWrites.Version"/> has not moved, so a publisher polling an idle daemon does no reads.</summary>
/// <typeparam name="T">What the read produces. Hold only values read from the database in it: anything that changes without a write, such as the time or the activity tracker, must be read afresh by the caller.</typeparam>
internal sealed class StoreReadCache<T>(IDatabaseWrites writes)
    where T : notnull
{
    private readonly object gate = new();
    private Option<(long Version, T Value)> cached = Option.None<(long Version, T Value)>();

    /// <summary>Returns the cached value when no write was committed since it was read; otherwise runs <paramref name="read"/> and keeps its success. Safe to call from any thread.</summary>
    /// <param name="read">Reads the value from the database.</param>
    /// <param name="cancellationToken">Cancelling surfaces as <see cref="OperationCanceledException"/>, also when the cached value would have been returned.</param>
    /// <returns>The cached or fresh value, or the failure of <paramref name="read"/>, which is never kept, so the next call reads again.</returns>
    public async Task<Result<T, IpcError>> ReadAsync(Func<CancellationToken, Task<Result<T, IpcError>>> read, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = writes.Version;
        if (CachedAt(version).Value is Some<T> hit) return new Ok<T>(hit.Value);

        var result = await read(cancellationToken);
        if (result.Value is Ok<T> ok) Keep(version, ok.Value);

        return result;
    }

    private Option<T> CachedAt(long version)
    {
        lock (gate)
        {
            return cached.Value is Some<(long Version, T Value)> entry && entry.Value.Version == version ? Option.Some(entry.Value.Value) : Option.None<T>();
        }
    }

    private void Keep(long version, T value)
    {
        lock (gate)
        {
            cached = Option.Some((version, value));
        }
    }
}

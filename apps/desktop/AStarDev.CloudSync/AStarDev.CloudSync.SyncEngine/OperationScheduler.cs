using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Runs queued operations through <see cref="IOperationExecutor"/> on a configurable number of workers, retrying failures with exponential back-off and dead-lettering the ones that run out of attempts.</summary>
internal sealed class OperationScheduler(IOperationStore store, IOperationExecutor executor, IOptions<OperationSchedulerOptions> options, TimeProvider timeProvider, ILogger<OperationScheduler> logger) : IAsyncDisposable
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Task[] workers = [];

/// <summary>Starts the workers. Calling it again while running does nothing.</summary>
    public void Start()
    {
        lock (gate)
        {
            if (cancellation is not null) return;

            var source = new CancellationTokenSource();
            cancellation = source;
            workers = [.. Enumerable.Range(0, Math.Max(1, options.Value.WorkerCount)).Select(_ => Task.Run(() => WorkAsync(source.Token), CancellationToken.None))];
        }
    }

/// <summary>Cancels the workers and waits for them to end; an operation being executed stays running. Safe to call twice or before <see cref="Start"/>.</summary>
    /// <param name="cancellationToken">Cancelling stops the waiting, not the stop.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? source;
        Task[] running;
        lock (gate)
        {
            source = cancellation;
            running = workers;
            cancellation = null;
            workers = [];
        }

        if (source is null) return;

        await source.CancelAsync();
        await Task.WhenAll(running.Select(worker => Task.WhenAny(worker))).WaitAsync(cancellationToken);
        source.Dispose();
    }

/// <inheritdoc/>
    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);

/// <summary>Claims one due operation, executes it and records the outcome.</summary>
    /// <param name="cancellationToken">Cancelling surfaces as <see cref="OperationCanceledException"/>; a claimed operation is then left running.</param>
    /// <returns><see langword="true"/> when an operation was processed, <see langword="false"/> when none was due, or the <see cref="StoreError"/> that stopped the queue.</returns>
    public async Task<Result<bool, StoreError>> RunOnceAsync(CancellationToken cancellationToken)
    {
        var claimed = await store.ClaimNextAsync(timeProvider.GetUtcNow(), 1, cancellationToken);

        return claimed switch
        {
            Ok<IReadOnlyList<Operation>> ok => ok.Value.Count == 0 ? new Ok<bool>(false) : await ExecuteAsync(ok.Value[0], cancellationToken),
            Fail<StoreError> fail => fail
        };
    }

    private async Task WorkAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var outcome = await RunOnceAsync(cancellationToken);
            if (outcome.Value is Ok<bool> { Value: true }) continue;

            if (outcome.Value is Fail<StoreError> fail) logger.LogError("The operation queue could not be read or written: {Error}", fail.Error.Value);

            await Task.Delay(options.Value.PollInterval, timeProvider, cancellationToken);
        }
    }

    private async Task<Result<bool, StoreError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(operation, cancellationToken);
        var settled = result switch
        {
            Ok<Unit> => await store.CompleteAsync(operation.Id, cancellationToken),
            Fail<ProviderError> fail => await FailAsync(operation, fail.Error, cancellationToken)
        };

        return settled switch
        {
            Ok<Unit> => new Ok<bool>(true),
            Fail<StoreError> fail => fail
        };
    }

    private async Task<Result<Unit, StoreError>> FailAsync(Operation operation, ProviderError error, CancellationToken cancellationToken)
    {
        var retryAtUtc = RetryDelay(operation, error) is { } delay ? timeProvider.GetUtcNow() + delay : (DateTimeOffset?)null;
        logger.LogWarning("Operation {Operation} failed on attempt {Attempt}, {Outcome}: {Error}", operation.Id.Value, operation.Attempts, retryAtUtc is null ? "dead-lettered" : $"retrying at {retryAtUtc:O}", error.Value);

        return await store.FailAsync(operation.Id, error.Value?.ToString() ?? string.Empty, retryAtUtc, cancellationToken);
    }

    private TimeSpan? RetryDelay(Operation operation, ProviderError error)
        => operation.Attempts >= Math.Max(1, options.Value.MaxAttempts)
            ? null
            : error switch
            {
                RateLimited limited => limited.RetryAfter,
                Transient => options.Value.RetryBaseDelay * Math.Pow(2, operation.Attempts - 1),
                AuthenticationFailed => null,
                Permanent => null,
                InvalidDeltaToken => null
            };
}

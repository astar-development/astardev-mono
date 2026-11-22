using System.Diagnostics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Wraps the real executor so that the baseline staged for an operation is applied only after the executor reports success. Executors never touch sync state.</summary>
internal sealed class KnownSnapshotCommitter(IOperationExecutor inner, IKnownSnapshotStore knownStore) : IOperationExecutor
{
    /// <inheritdoc/>
    /// <remarks>A failed run commits nothing. When the work succeeded but the commit could not be stored the result is a <see cref="Transient"/> failure, so the operation runs again; operations are idempotent.</remarks>
    public async Task<Result<Unit, ProviderError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(operation, cancellationToken);
        if (result.Value is Fail<ProviderError>) return result;

        using var span = WorkflowTelemetry.StartCommit(operation);

        return await knownStore.CommitAsync(operation.Id, cancellationToken) switch
        {
            Ok<Unit> => result,
            Fail<StoreError> fail => CommitFailed(span, operation, fail.Error)
        };
    }

    private static Fail<ProviderError> CommitFailed(Activity? span, Operation operation, StoreError error)
    {
        WorkflowTelemetry.Fail(span, error.Value?.GetType().Name ?? "unknown", "The baseline could not be stored.");

        return new Fail<ProviderError>(new Transient($"The baseline of operation {operation.Id.Value} could not be stored: {error.Value}"));
    }
}

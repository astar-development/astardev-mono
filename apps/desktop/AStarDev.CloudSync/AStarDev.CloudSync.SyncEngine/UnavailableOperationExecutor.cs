using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Stands in until a slice registers a real <see cref="IOperationExecutor"/>; it fails every operation permanently, so none is silently lost or reported done.</summary>
internal sealed class UnavailableOperationExecutor : IOperationExecutor
{
    /// <inheritdoc/>
    public Task<Result<Unit, ProviderError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<Unit, ProviderError>>(new Fail<ProviderError>(new Permanent("No operation executor is registered.")));
    }
}

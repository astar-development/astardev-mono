using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class ScriptedOperationExecutor(Func<Operation, Result<Unit, ProviderError>> script) : IOperationExecutor
{
    private readonly List<OperationId> executed = [];

    public IReadOnlyList<OperationId> Executed
    {
        get
        {
            lock (executed) return [.. executed];
        }
    }

    public Task<Result<Unit, ProviderError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (executed) executed.Add(operation.Id);

        return Task.FromResult(script(operation));
    }
}

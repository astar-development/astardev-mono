using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration.DaemonTests;

internal sealed class RecordingIpcServer : IIpcServer
{
    public List<IEvent> Published { get; } = [];

    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand
    {
    }

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult>
    {
    }

    public Task<Result<Unit, IpcError>> StartAsync(CancellationToken cancellationToken) => Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));

    public Task<Result<Unit, IpcError>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        cancellationToken.ThrowIfCancellationRequested();
        Published.Add(@event);

        return Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    }

    public Task<Result<Unit, IpcError>> StopAsync(CancellationToken cancellationToken) => Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

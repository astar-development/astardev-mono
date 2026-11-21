using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class FakeIpcServer(List<string> ran) : IIpcServer
{
    public bool Stopped { get; private set; }

    public void MapCommand<TCommand>(Func<TCommand, CancellationToken, Task<Result<CommandResult, IpcError>>> handler)
        where TCommand : ICommand
    {
    }

    public void MapQuery<TQuery, TResult>(Func<TQuery, CancellationToken, Task<Result<TResult, IpcError>>> handler)
        where TQuery : IQuery<TResult>
    {
    }

    public Task<Result<Unit, IpcError>> StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ran.Add("ipc");

        return Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    }

    public Task<Result<Unit, IpcError>> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
        => Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));

    public Task<Result<Unit, IpcError>> StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stopped = true;

        return Task.FromResult<Result<Unit, IpcError>>(new Ok<Unit>(default));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class FakeStage(string name, List<string> ran, bool fails = false, Action? onExecute = null) : IStartupStage
{
    public string Name { get; } = name;

    public CancellationToken ReceivedToken { get; private set; }

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        ran.Add(Name);
        ReceivedToken = cancellationToken;
        onExecute?.Invoke();

        Result<Unit, StartupError> result = fails ? new Fail<StartupError>(new StartupError(Name, "boom")) : new Ok<Unit>(default);
        return Task.FromResult(result);
    }
}

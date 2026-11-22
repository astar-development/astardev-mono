using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>
/// Stage 1: host started, configuration loaded, logging initialised, and this process is the only daemon for the user.
/// A second daemon fails here, before it touches the database or the IPC socket of the running one.
/// </summary>
/// <remarks>Holds the instance lock until the host disposes it, so the lock lasts as long as the daemon runs.</remarks>
internal sealed class ProcessStartStage(ISingleInstanceGuard guard, SingleInstanceOptions options) : IStartupStage, IAsyncDisposable
{
    private Option<PrimaryInstance> held = Option.None<PrimaryInstance>();

    /// <inheritdoc/>
    public string Name => ApplicationConstants.ProcessStartStageName;

    /// <inheritdoc/>
    /// <returns>Success when this process holds the instance lock. A <see cref="StartupError"/> when another daemon is running, holds the lock without answering, or the lock cannot be created.</returns>
    public async Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Outcome(await guard.ClaimAsync(options, cancellationToken));

    /// <summary>Releases the instance lock and removes the hand-off socket. Safe to call when the lock was never held.</summary>
    public ValueTask DisposeAsync() => held.Match(primary => primary.DisposeAsync(), () => ValueTask.CompletedTask);

    private Result<Unit, StartupError> Outcome(InstanceClaim claim)
        => claim switch
        {
            InstanceClaim.Primary primary => Keep(primary),
            InstanceClaim.HandedOff => Refuse(ApplicationConstants.AlreadyRunningText),
            InstanceClaim.NotAnswered => Refuse(ApplicationConstants.NotAnsweringText),
            _ => Refuse(ApplicationConstants.InstanceLockUnavailableText)
        };

    private Result<Unit, StartupError> Keep(InstanceClaim.Primary primary)
    {
        held = Option.Some(primary.Instance);

        return new Ok<Unit>(default);
    }

    private Result<Unit, StartupError> Refuse(string cause) => new Fail<StartupError>(new StartupError(Name, cause));
}

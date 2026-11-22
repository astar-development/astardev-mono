using AStarDev.FunctionalParadigm;

namespace AStarDev.SingleInstance;

/// <summary>Runs socket work that may fail or be cancelled and turns any outcome into an <see cref="Exceptional{T}"/>, so a timeout never escapes as an exception.</summary>
internal static class Attempt
{
    /// <summary>Runs the operation and returns its value, or the exception that stopped it (a <see cref="TaskCanceledException"/> when it was cancelled). The caller checks its own token to tell a user cancel from a timeout.</summary>
    /// <param name="operation">The work to run.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public static async Task<Exceptional<T>> RunAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var running = Task.Run(operation, cancellationToken);
        await ((Task)running).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        return running.IsCompletedSuccessfully
            ? Exceptional.Success(await running.ConfigureAwait(false))
            : Exceptional.Failure<T>(running.Exception?.InnerException ?? new TaskCanceledException(running));
    }
}

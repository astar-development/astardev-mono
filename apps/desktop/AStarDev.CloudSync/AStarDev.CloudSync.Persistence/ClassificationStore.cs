using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Runs one classification store call on its own short-lived context, turning an exception into <see cref="StorageFailed"/> or the error the caller maps it to.</summary>
internal static class ClassificationStore
{
    /// <summary>Runs <paramref name="work"/> on a new context.</summary>
    /// <typeparam name="T">The success value.</typeparam>
    /// <param name="contextFactory">Creates the context.</param>
    /// <param name="work">The call; its own failures come back as values.</param>
    /// <param name="onException">Maps an exception that escapes <paramref name="work"/>; defaults to <see cref="StorageFailed"/>.</param>
    /// <param name="cancellationToken">Cancellation propagates as <see cref="OperationCanceledException"/>.</param>
    /// <returns>What <paramref name="work"/> returned, or the mapped exception.</returns>
    public static async Task<Result<T, ClassificationError>> RunAsync<T>(IDbContextFactory<CloudSyncDbContext> contextFactory, Func<CloudSyncDbContext, CancellationToken, Task<Result<T, ClassificationError>>> work, Func<Exception, ClassificationError>? onException, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<Result<T, ClassificationError>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await work(context, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => onException?.Invoke(exception) ?? new StorageFailed(exception.Message)) switch
        {
            Ok<Result<T, ClassificationError>> ok => ok.Value,
            Fail<ClassificationError> fail => fail
        };
    }
}

using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IJobAccounts"/>. Each call uses its own short-lived context.</summary>
internal sealed class JobAccounts(IDbContextFactory<CloudSyncDbContext> contextFactory) : IJobAccounts
{
    /// <inheritdoc/>
    public async Task<Result<string, StoreError>> AccountIdentifierAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);

            return await context.SyncJobs.AsNoTracking().Where(job => job.Id == jobId).Join(context.Accounts, job => job.AccountId, account => account.Id, (job, account) => account.AccountIdentifier).SingleOrDefaultAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => (StoreError)new StorageFailed(exception.Message)) switch
        {
            Ok<string> { Value: { } identifier } => new Ok<string>(identifier),
            Ok<string> => new Fail<StoreError>(new SyncJobNotFound(jobId)),
            Fail<StoreError> fail => fail
        };
    }
}

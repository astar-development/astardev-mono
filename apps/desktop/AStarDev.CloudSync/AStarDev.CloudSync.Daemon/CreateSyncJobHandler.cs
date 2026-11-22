using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="CreateSyncJob"/> by checking the local folder and storing the job in the <see cref="ISyncJobStore"/>. Connecting an account never reaches this handler.</summary>
internal sealed class CreateSyncJobHandler(IAccountStore accounts, ISyncJobStore jobs, TimeProvider timeProvider, ILogger<CreateSyncJobHandler> logger)
{
    /// <summary>Validates the local folder first, so a bad path is reported before anything is looked up or written, then stores the job.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once the job is stored, <see cref="CommandOutcome.ValidationError"/> when the local folder is not an absolute path to an existing folder, or
    /// <see cref="CommandOutcome.Rejected"/> with a message for the user when the account is unknown, a job already exists (the MVP allows one) or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(CreateSyncJob command, CancellationToken cancellationToken)
        => ValidateLocalRoot(command.LocalRootPath) switch
        {
            Ok<Unit> => await FindAccountAsync(command, cancellationToken),
            Fail<string> fail => Respond(CommandOutcome.ValidationError, fail.Error)
        };

    private static Result<Unit, string> ValidateLocalRoot(string path)
        => !Path.IsPathFullyQualified(path)
            ? new Fail<string>("The local folder must be an absolute path.")
            : !Directory.Exists(path)
                ? new Fail<string>("The local folder does not exist.")
                : new Ok<Unit>(default);

    private async Task<Result<CommandResult, IpcError>> FindAccountAsync(CreateSyncJob command, CancellationToken cancellationToken)
        => await accounts.GetAsync(new AccountId(command.AccountId), cancellationToken) switch
        {
            Ok<Account> account => await StoreAsync(account.Value, command, cancellationToken),
            Fail<StoreError> fail => Respond(CommandOutcome.Rejected, StoreErrorMessages.Describe(fail.Error, "saved", "sync job"))
        };

    private async Task<Result<CommandResult, IpcError>> StoreAsync(Account account, CreateSyncJob command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, account.DisplayName, command.LocalRootPath, new RemoteItemId(ApplicationConstants.DriveRootItemId), BootstrapModes.ToDomain(command.BootstrapMode), true, now, now);

        return await jobs.AddAsync(job, cancellationToken) switch
        {
            Ok<Unit> => Accept(job),
            Fail<StoreError> fail => Respond(CommandOutcome.Rejected, StoreErrorMessages.Describe(fail.Error, "saved", "sync job"))
        };
    }

    private Ok<CommandResult> Accept(SyncJob job)
    {
        logger.LogInformation("Created sync job {Job} in {Mode} mode", job.Id.Value, job.BootstrapMode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }

    private Ok<CommandResult> Respond(CommandOutcome outcome, string message)
    {
        logger.LogWarning("Creating a sync job failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(outcome, message));
    }
}

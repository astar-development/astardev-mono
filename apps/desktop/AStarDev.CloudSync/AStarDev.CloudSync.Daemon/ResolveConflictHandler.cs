using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="ResolveConflict"/> by handing the decision to the <see cref="ConflictResolver"/>.</summary>
internal sealed class ResolveConflictHandler(ConflictResolver resolver)
{
    /// <summary>Resolves every listed conflict with the one choice.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.ValidationError"/> when no id is given; <see cref="CommandOutcome.Accepted"/> when every conflict was handled;
    /// otherwise <see cref="CommandOutcome.Rejected"/> with one sentence per conflict that was not, while the rest stay resolved. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(ResolveConflict command, CancellationToken cancellationToken)
    {
        if (command.ConflictIds.Count == 0) return new Ok<CommandResult>(new CommandResult(CommandOutcome.ValidationError, "Choose at least one conflict to resolve."));

        var outcomes = await resolver.ResolveAsync([.. command.ConflictIds.Select(id => new ConflictId(id))], ToResolution(command.Choice), cancellationToken);
        var failures = outcomes.Select(outcome => outcome.Value).OfType<Fail<ResolutionError>>().Select(fail => ResolutionMessages.Describe(fail.Error)).Distinct().ToList();

        return new Ok<CommandResult>(failures.Count == 0 ? new CommandResult(CommandOutcome.Accepted) : new CommandResult(CommandOutcome.Rejected, string.Join(' ', failures)));
    }

    private static ConflictResolution ToResolution(ConflictChoice choice) => choice switch
    {
        ConflictChoice.KeepLocal => ConflictResolution.KeepLocal,
        ConflictChoice.KeepRemote => ConflictResolution.KeepRemote,
        ConflictChoice.KeepBoth => ConflictResolution.KeepBoth,
        _ => ConflictResolution.Defer
    };
}

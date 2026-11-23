using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="SetSetting"/> by saving the value in the <see cref="ISettingsStore"/>.</summary>
internal sealed class SetSettingHandler(ISettingsStore store, ILogger<SetSettingHandler> logger)
{
    /// <summary>Saves the setting.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the setting is unknown, the value is not allowed or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(SetSetting command, CancellationToken cancellationToken)
        => await store.SetAsync(command.Name, command.Value, cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved", "setting"))
        };

    private Ok<CommandResult> Reject(string message)
    {
        logger.LogWarning("Saving a setting failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}

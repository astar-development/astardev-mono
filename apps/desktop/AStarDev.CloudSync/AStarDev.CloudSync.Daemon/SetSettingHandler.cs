using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="SetSetting"/> by saving the value in the <see cref="ISettingsStore"/>.</summary>
internal sealed class SetSettingHandler(ISettingsStore store, AuditJournal audit, ILogger<SetSettingHandler> logger)
{
    /// <summary>Saves the setting.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once stored and recorded in the audit trail, or <see cref="CommandOutcome.Rejected"/> with a message for the user when the setting is unknown, the value is not allowed or the store fails.
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(SetSetting command, CancellationToken cancellationToken)
        => await store.SetAsync(command.Name, command.Value, cancellationToken) switch
        {
            Ok<Unit> => await RecordAsync(command, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved", "setting"), ErrorInfo.CodeOf(fail.Error.Value))
        };

    private async Task<Result<CommandResult, IpcError>> RecordAsync(SetSetting command, CancellationToken cancellationToken)
    {
        await audit.RecordAsync("SettingChanged", Guid.Empty, new { name = command.Name, value = command.Value }, cancellationToken);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted));
    }

    private Ok<CommandResult> Reject(string message, ErrorCode errorCode)
    {
        DaemonLog.SetSettingFailed(logger, message, errorCode);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}

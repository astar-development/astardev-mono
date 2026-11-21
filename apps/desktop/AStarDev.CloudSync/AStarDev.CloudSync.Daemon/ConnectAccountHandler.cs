using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="ConnectAccount"/> by signing in through <see cref="ICloudProvider"/> and announcing the result with <see cref="AccountConnected"/>.</summary>
internal sealed class ConnectAccountHandler(ICloudProvider provider, IIpcServer server, ILogger<ConnectAccountHandler> logger)
{
    /// <summary>Signs in and, on success, publishes <see cref="AccountConnected"/> before answering.</summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once connected, or <see cref="CommandOutcome.Rejected"/> with the provider's failure worded for the user. An <see cref="IpcError"/>
    /// only when the event could not be published. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(ConnectAccount command, CancellationToken cancellationToken)
        => await provider.ConnectAsync(cancellationToken) switch
        {
            Ok<ProviderAccount> account => await AnnounceAsync(account.Value, cancellationToken),
            Fail<ProviderError> fail => Reject(fail.Error)
        };

    private async Task<Result<CommandResult, IpcError>> AnnounceAsync(ProviderAccount account, CancellationToken cancellationToken)
    {
        logger.LogInformation("Connected account {Account}", account.DisplayName);

        return await server.PublishAsync(new AccountConnected(account.DisplayName), cancellationToken) switch
        {
            Ok<Unit> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
            Fail<IpcError> fail => fail
        };
    }

    private Ok<CommandResult> Reject(ProviderError error)
    {
        var message = ProviderErrorMessages.Describe(error);
        logger.LogWarning("Connecting an account failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}

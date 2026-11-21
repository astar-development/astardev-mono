using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="ConnectAccount"/> by signing in through <see cref="ICloudProvider"/>, keeping the account in the <see cref="IAccountStore"/> and announcing the result with <see cref="AccountConnected"/>.</summary>
internal sealed class ConnectAccountHandler(ICloudProvider provider, IAccountStore store, IIpcServer server, TimeProvider timeProvider, ILogger<ConnectAccountHandler> logger)
{
    /// <summary>
    /// Signs in and, on success, stores the account (or marks the already stored one <see cref="AccountStatus.Connected"/>, which is how a user signs in again) and publishes <see cref="AccountConnected"/> before answering.
    /// </summary>
    /// <returns>
    /// <see cref="CommandOutcome.Accepted"/> once connected and stored, or <see cref="CommandOutcome.Rejected"/> with the provider's or the store's failure worded for the user. An <see cref="IpcError"/>
    /// only when the event could not be published. Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </returns>
    public async Task<Result<CommandResult, IpcError>> HandleAsync(ConnectAccount command, CancellationToken cancellationToken)
        => await provider.ConnectAsync(cancellationToken) switch
        {
            Ok<ProviderAccount> account => await KeepAsync(account.Value, cancellationToken),
            Fail<ProviderError> fail => Reject(ProviderErrorMessages.Describe(fail.Error))
        };

    private async Task<Result<CommandResult, IpcError>> KeepAsync(ProviderAccount account, CancellationToken cancellationToken)
        => await StoreAsync(account, cancellationToken) switch
        {
            Ok<Unit> => await AnnounceAsync(account, cancellationToken),
            Fail<StoreError> fail => Reject(StoreErrorMessages.Describe(fail.Error, "saved"))
        };

    private async Task<Result<Unit, StoreError>> StoreAsync(ProviderAccount account, CancellationToken cancellationToken)
        => await store.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Account>> list => await AddOrRestoreAsync(account, list.Value, cancellationToken),
            Fail<StoreError> fail => fail
        };

    private async Task<Result<Unit, StoreError>> AddOrRestoreAsync(ProviderAccount account, IReadOnlyList<Account> stored, CancellationToken cancellationToken)
        => stored.FirstOrDefault(known => known.ProviderType == ApplicationConstants.OneDriveProviderType && known.AccountIdentifier == account.AccountIdentifier) switch
        {
            { } known => await store.UpdateStatusAsync(known.Id, AccountStatus.Connected, cancellationToken),
            null => await store.AddAsync(new Account(new AccountId(Guid.NewGuid()), ApplicationConstants.OneDriveProviderType, account.DisplayName, account.AccountIdentifier, timeProvider.GetUtcNow()), cancellationToken)
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

    private Ok<CommandResult> Reject(string message)
    {
        logger.LogWarning("Connecting an account failed: {Message}", message);

        return new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, message));
    }
}

using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 3: load the stored accounts and sign each one in again from its stored token, without a browser. Sync jobs, pending operations and open conflicts arrive in later slices.</summary>
internal sealed class StateRecoveryStage(IAccountStore store, ICloudProvider provider, IDatabaseState databaseState, ILogger<StateRecoveryStage> logger) : IStartupStage
{
    /// <inheritdoc/>
    public string Name => ApplicationConstants.StateRecoveryStageName;

    /// <inheritdoc/>
    /// <remarks>
    /// A provider that refuses the stored token marks the account <see cref="AccountStatus.ReauthenticationRequired"/>, which pauses it, and startup carries on. A provider that cannot be reached leaves the status as it was,
    /// so being offline at boot does not ask the user to sign in. A database in recovery mode is skipped, because its accounts cannot be trusted. Accounts that cannot be read or updated are a <see cref="StartupError"/>.
    /// </remarks>
    public async Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (databaseState.Mode == DatabaseMode.Recovery) return Skip();

        return await store.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Account>> accounts => await RestoreAllAsync(accounts.Value, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };
    }

    private Ok<Unit> Skip()
    {
        StartupLog.AccountsNotReconnected(logger);

        return new Ok<Unit>(default);
    }

    private async Task<Result<Unit, StartupError>> RestoreAllAsync(IReadOnlyList<Account> accounts, CancellationToken cancellationToken)
    {
        foreach (var account in accounts)
        {
            if (await RestoreAsync(account, cancellationToken) is Fail<StoreError> fail) return Failed(fail.Error);
        }

        return new Ok<Unit>(default);
    }

    private async Task<Result<Unit, StoreError>> RestoreAsync(Account account, CancellationToken cancellationToken)
        => await provider.ReconnectAsync(account.AccountIdentifier, cancellationToken) switch
        {
            Ok<ProviderAccount> => await MarkAsync(account, AccountStatus.Connected, cancellationToken),
            Fail<ProviderError> { Error: AuthenticationFailed refused } => await RequireReauthenticationAsync(account, refused, cancellationToken),
            Fail<ProviderError> fail => Unreachable(account, fail.Error)
        };

    private async Task<Result<Unit, StoreError>> RequireReauthenticationAsync(Account account, AuthenticationFailed refused, CancellationToken cancellationToken)
    {
        StartupLog.AccountMustSignInAgain(logger, account.Id.Value, refused.Message);

        return await MarkAsync(account, AccountStatus.ReauthenticationRequired, cancellationToken);
    }

    private Ok<Unit> Unreachable(Account account, ProviderError error)
    {
        StartupLog.AccountReconnectDeferred(logger, account.Id.Value, ProviderErrorMessages.Describe(error));

        return new Ok<Unit>(default);
    }

    private async Task<Result<Unit, StoreError>> MarkAsync(Account account, AccountStatus status, CancellationToken cancellationToken)
    {
        if (account.Status == status) return new Ok<Unit>(default);

        return await store.UpdateStatusAsync(account.Id, status, cancellationToken);
    }

    private Fail<StartupError> Failed(StoreError error) => new(new StartupError(Name, StoreErrorMessages.Describe(error, "read or updated")));
}

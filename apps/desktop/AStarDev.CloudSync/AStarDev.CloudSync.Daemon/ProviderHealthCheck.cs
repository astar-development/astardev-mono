using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Reports whether the stored accounts can still be used: degraded while any needs the user to sign in again, unhealthy when the accounts cannot be read.</summary>
internal sealed class ProviderHealthCheck(IAccountStore store) : IHealthCheck
{
    /// <inheritdoc/>
    /// <remarks>No stored account is healthy: there is nothing the provider could be failing for. The description of a degraded result names the accounts to sign in again.</remarks>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await store.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<Account>> accounts => Describe(accounts.Value),
            Fail<StoreError> fail => HealthCheckResult.Unhealthy(StoreErrorMessages.Describe(fail.Error, "read"))
        };

    private static HealthCheckResult Describe(IReadOnlyList<Account> accounts)
        => accounts.Where(account => account.Status == AccountStatus.ReauthenticationRequired).Select(account => account.DisplayName).ToList() switch
        {
            [] => HealthCheckResult.Healthy(),
            var names => HealthCheckResult.Degraded($"These accounts must sign in again: {string.Join(", ", names)}")
        };
}

using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="ISettingsStore"/>. Each call uses its own short-lived context, so calls may overlap.</summary>
internal sealed class SettingsStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : ISettingsStore
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<string, string>, StoreError>> GetAllAsync(CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var saved = await context.Settings.AsNoTracking().ToDictionaryAsync(entity => entity.Name, entity => entity.Value, token).ConfigureAwait(false);

            return (IReadOnlyDictionary<string, string>)SettingDefinitions.Defaults.ToDictionary(pair => pair.Key, pair => saved.GetValueOrDefault(pair.Key, pair.Value));
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, StoreError>> SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!SettingDefinitions.IsKnown(name)) return new Fail<StoreError>(new UnknownSetting(name));
        if (!SettingDefinitions.IsValid(name, value)) return new Fail<StoreError>(new InvalidSettingValue(name, value));

        var outcome = await Try.RunAsync(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var entity = await context.Settings.SingleOrDefaultAsync(setting => setting.Name == name, token).ConfigureAwait(false);
            if (entity is null) context.Settings.Add(new SettingEntity { Name = name, Value = value });
            else entity.Value = value;

            await context.SaveChangesAsync(token).ConfigureAwait(false);

            return default(Unit);
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(ToStorageFailure);
    }

    private static StoreError ToStorageFailure(Exception exception) => new StorageFailed(exception.Message);
}

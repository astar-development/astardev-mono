using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetSettings"/> from the <see cref="ISettingsStore"/>.</summary>
internal sealed class SettingsQuery(ISettingsStore store)
{
    /// <summary>Reads every setting.</summary>
    /// <returns>Every known setting with its saved value or default, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<SettingsView, IpcError>> HandleAsync(GetSettings query, CancellationToken cancellationToken)
        => await store.GetAllAsync(cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<string, string>> settings => new Ok<SettingsView>(new SettingsView(settings.Value)),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "settings")))
        };
}

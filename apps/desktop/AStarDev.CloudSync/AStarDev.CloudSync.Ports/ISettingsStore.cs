using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where user settings are kept between runs. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Reads every setting named in <see cref="SettingNames"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>One entry per known setting: the saved value, or its documented default when never saved, so a fresh database returns every default. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyDictionary<string, string>, StoreError>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Saves one setting, replacing any earlier value.</summary>
    /// <param name="name">One of <see cref="SettingNames"/>.</param>
    /// <param name="value">The new value, in the form the setting documents.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the old value.</param>
    /// <returns>Success, <see cref="UnknownSetting"/> when the name is not a known setting, <see cref="InvalidSettingValue"/> when the value does not fit the setting, or <see cref="StorageFailed"/>. Nothing is written on failure.</returns>
    Task<Result<Unit, StoreError>> SetAsync(string name, string value, CancellationToken cancellationToken);
}

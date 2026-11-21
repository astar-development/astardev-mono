using System.Globalization;

namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>Formats with one fixed <see cref="CultureInfo"/>, chosen when the service is created.</summary>
/// <param name="cultureName">A culture name such as <c>en-GB</c>. An unknown name gives the invariant culture rather than failing.</param>
internal sealed class CultureService(string cultureName) : ICultureService
{
    /// <inheritdoc/>
    public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(cultureName);

    /// <inheritdoc/>
    public string FormatDate(DateTimeOffset value) => value.ToString("d", Culture);

    /// <inheritdoc/>
    public string FormatTime(DateTimeOffset value) => value.ToString("t", Culture);

    /// <inheritdoc/>
    public string FormatNumber(long value) => value.ToString("N0", Culture);

    /// <inheritdoc/>
    public string FormatValue(object? value) => value switch
    {
        null => string.Empty,
        DateTimeOffset moment => FormatDate(moment),
        int or long or short or byte => FormatNumber(Convert.ToInt64(value, Culture)),
        IFormattable formattable => formattable.ToString(null, Culture),
        _ => value.ToString() ?? string.Empty
    };
}

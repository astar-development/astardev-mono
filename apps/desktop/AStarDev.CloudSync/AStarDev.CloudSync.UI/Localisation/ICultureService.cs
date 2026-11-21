using System.Globalization;

namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>Owns date, time and number formatting, so view models hand over raw values and never format them.</summary>
internal interface ICultureService
{
    /// <summary>The culture every value is formatted with. Independent of the language of the texts.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Formats the calendar date of <paramref name="value"/> in the short date pattern of <see cref="Culture"/>. The offset of the value is kept, not converted to local time.</summary>
    public string FormatDate(DateTimeOffset value);

    /// <summary>Formats the time of day of <paramref name="value"/> in the short time pattern of <see cref="Culture"/>. The offset of the value is kept, not converted to local time.</summary>
    public string FormatTime(DateTimeOffset value);

    /// <summary>Formats <paramref name="value"/> with the digit grouping of <see cref="Culture"/> and no decimals.</summary>
    public string FormatNumber(long value);

    /// <summary>Formats any value with <see cref="Culture"/>: dates, times and numbers by culture rules, anything else by <c>ToString</c>; <see langword="null"/> is empty text.</summary>
    public string FormatValue(object? value);
}

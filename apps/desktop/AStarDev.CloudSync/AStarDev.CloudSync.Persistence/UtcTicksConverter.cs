using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Stores a <see cref="DateTimeOffset"/> as its UTC ticks, so SQLite compares and orders instants to the full 100 ns. The offset is not kept: values read back are always UTC.</summary>
internal sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
{
    /// <summary>The stored form of <paramref name="value"/>, for parameters of raw SQL that compare with a converted column.</summary>
    /// <param name="value">The instant, in any offset.</param>
    public static long ToStored(DateTimeOffset value) => value.UtcTicks;
}

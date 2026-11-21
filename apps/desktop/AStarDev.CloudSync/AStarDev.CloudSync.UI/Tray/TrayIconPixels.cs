using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>Draws the tray icon for each trust level as raw pixels: a different shape and colour per level, so the state reads without colour.</summary>
internal static class TrayIconPixels
{
    /// <summary>The width and height of the icon in pixels.</summary>
    public const int Size = 32;

    private const double Centre = (Size - 1) / 2.0;
    private const double Radius = 14;

    /// <summary>Healthy is a solid disc, Active a ring, Degraded a diamond, Attention a triangle and Critical a cross.</summary>
    /// <param name="level">The level to draw.</param>
    /// <returns>Premultiplied BGRA pixels, row by row, <see cref="Size"/> by <see cref="Size"/>. Pixels outside the shape are fully transparent.</returns>
    public static byte[] Render(TrustLevel level)
    {
        var pixels = new byte[Size * Size * 4];
        var colour = ColourOf(level);
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                if (!Covers(level, x - Centre, y - Centre)) continue;

                var offset = ((y * Size) + x) * 4;
                pixels[offset] = colour.Blue;
                pixels[offset + 1] = colour.Green;
                pixels[offset + 2] = colour.Red;
                pixels[offset + 3] = 255;
            }

        return pixels;
    }

    private static bool Covers(TrustLevel level, double dx, double dy) => level switch
    {
        TrustLevel.Healthy => Math.Sqrt((dx * dx) + (dy * dy)) <= Radius,
        TrustLevel.Active => Math.Sqrt((dx * dx) + (dy * dy)) is var distance && distance <= Radius && distance >= Radius - 5,
        TrustLevel.Degraded => Math.Abs(dx) + Math.Abs(dy) <= Radius,
        TrustLevel.Attention => dy >= -Radius && dy <= Radius && Math.Abs(dx) <= (dy + Radius) / 2,
        _ => Math.Abs(Math.Abs(dx) - Math.Abs(dy)) <= 2.5 && Math.Abs(dx) <= Radius && Math.Abs(dy) <= Radius
    };

    private static (byte Blue, byte Green, byte Red) ColourOf(TrustLevel level) => level switch
    {
        TrustLevel.Healthy => (0x50, 0xB0, 0x4C),
        TrustLevel.Active => (0xF3, 0x96, 0x21),
        TrustLevel.Degraded => (0x3B, 0xC1, 0xFF),
        TrustLevel.Attention => (0x00, 0x98, 0xFF),
        _ => (0x36, 0x43, 0xF4)
    };
}

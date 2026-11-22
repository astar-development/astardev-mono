using System.Globalization;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Names the extra copy that keeps the local version of a conflicted file beside the remote one. Pure: no I/O, no state.</summary>
internal static class KeepBoth
{
    private const string StampFormat = "yyyy-MM-dd HHmmss";

    /// <summary>Builds <c>&lt;name&gt; (Conflict - yyyy-MM-dd HHmmss)&lt;ext&gt;</c> in the folder of the original, with the time in UTC so the name does not depend on the machine's time zone.</summary>
    /// <param name="original">The conflicted file; its folder and extension are kept.</param>
    /// <param name="now">When the copy is made.</param>
    /// <param name="exists">Tells whether a path is already taken. A taken name gets <c> (2)</c>, <c> (3)</c> and so on before the extension, so nothing is ever overwritten.</param>
    /// <returns>A path for which <paramref name="exists"/> was false.</returns>
    public static RelativePath NameCopy(RelativePath original, DateTimeOffset now, Func<RelativePath, bool> exists)
    {
        var text = original.Value;
        var split = text.LastIndexOf('/') + 1;
        var file = text[split..];
        var extension = Path.GetExtension(file);
        var stem = $"{text[..split]}{file[..^extension.Length]} (Conflict - {now.UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture)})";
        var candidate = new RelativePath($"{stem}{extension}");

        for (var suffix = 2; exists(candidate); suffix++) candidate = new RelativePath($"{stem} ({suffix}){extension}");

        return candidate;
    }
}

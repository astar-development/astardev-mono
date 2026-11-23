namespace AStarDev.CloudSync.Daemon;

/// <summary>Writes CSV text per RFC 4180: comma separated, CRLF terminated, fields quoted when they need it.</summary>
internal static class CsvRows
{
    private const string LineEnd = "\r\n";

    /// <summary>Joins <paramref name="header"/> and <paramref name="rows"/> into one CSV text, each row ending in a line break.</summary>
    public static string Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
        => string.Concat(new[] { header }.Concat(rows).Select(row => string.Join(',', row.Select(Field)) + LineEnd));

    private static string Field(string value)
        => value.AsSpan().IndexOfAny(",\"\r\n") < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
}

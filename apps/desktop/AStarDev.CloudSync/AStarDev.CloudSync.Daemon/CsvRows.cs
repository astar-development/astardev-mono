namespace AStarDev.CloudSync.Daemon;

/// <summary>Writes CSV text per RFC 4180: comma separated, CRLF terminated, fields quoted when they need it and prefixed with <c>'</c> when they start with <c>=</c>, <c>+</c>, <c>-</c> or <c>@</c> so spreadsheets do not read them as formulas.</summary>
internal static class CsvRows
{
    private const string LineEnd = "\r\n";
    private const string FormulaStarts = "=+-@";

    /// <summary>Joins <paramref name="header"/> and <paramref name="rows"/> into one CSV text, each row ending in a line break.</summary>
    public static string Write(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
        => string.Concat(new[] { header }.Concat(rows).Select(row => string.Join(',', row.Select(Field)) + LineEnd));

    private static string Field(string value)
        => Quoted(Guarded(value));

    private static string Guarded(string value)
        => value.Length > 0 && FormulaStarts.Contains(value[0]) ? "'" + value : value;

    private static string Quoted(string value)
        => value.AsSpan().IndexOfAny(",\"\r\n") < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
}

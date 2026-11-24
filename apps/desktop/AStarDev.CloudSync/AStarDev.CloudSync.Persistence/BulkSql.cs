namespace AStarDev.CloudSync.Persistence;

/// <summary>Builds the parts of a multi-row statement. One statement for many rows is logged once, where a tracked save is logged once per row.</summary>
internal static class BulkSql
{
    /// <summary>Builds <c>({0},{1}),({2},{3})</c> for the given number of rows and columns, to pass to <see cref="System.Runtime.CompilerServices.FormattableStringFactory"/> so the values stay parameters.</summary>
    /// <param name="rows">How many rows.</param>
    /// <param name="columns">How many values per row.</param>
    /// <returns>The comma separated row placeholders.</returns>
    public static string Rows(int rows, int columns) => string.Join(',', Enumerable.Range(0, rows).Select(row => "(" + string.Join(',', Enumerable.Range(row * columns, columns).Select(position => "{" + position + "}")) + ")"));

    /// <summary>Writes an identity the way SQLite stores it: upper case text.</summary>
    /// <param name="value">The identity.</param>
    /// <returns>The stored text.</returns>
    public static string Text(Guid value) => value.ToString().ToUpperInvariant();
}

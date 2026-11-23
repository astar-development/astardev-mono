namespace AStarDev.CloudSync.IPC;

/// <summary>An export answering <see cref="ExportHistory"/> or <see cref="ExportAudit"/>.</summary>
/// <param name="Csv">The whole file as CSV text: a header row, then one row per entry, fields quoted when they hold a comma, quote or line break. A header alone means nothing matched.</param>
public sealed record ExportView(string Csv);

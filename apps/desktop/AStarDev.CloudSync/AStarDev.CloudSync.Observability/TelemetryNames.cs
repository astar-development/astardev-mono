namespace AStarDev.CloudSync.Observability;

internal static class TelemetryNames
{
    /// <summary>Wildcard that matches the name of every CloudSync activity source and meter, so a module needs no reference to Observability to be collected.</summary>
    public const string SourcePattern = "AStarDev.CloudSync.*";
}

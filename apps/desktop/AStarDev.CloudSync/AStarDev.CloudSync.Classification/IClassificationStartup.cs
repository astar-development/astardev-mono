namespace AStarDev.CloudSync.Classification;

/// <summary>Prepares classification once the database is ready.</summary>
public interface IClassificationStartup
{
    /// <summary>Applies the shipped seed when it is new. Nothing here is fatal: the app works without classification, so a failure is logged and the next start tries again. Cancellation propagates.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    Task RunAsync(CancellationToken cancellationToken);
}

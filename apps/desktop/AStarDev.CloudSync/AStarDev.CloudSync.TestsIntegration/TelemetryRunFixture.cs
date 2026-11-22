namespace AStarDev.CloudSync.TestsIntegration;

public sealed class TelemetryRunFixture : IAsyncLifetime
{
    internal TelemetryRun Run { get; } = new();

    public async ValueTask InitializeAsync() => await Run.DriveAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await Run.DisposeAsync();
}

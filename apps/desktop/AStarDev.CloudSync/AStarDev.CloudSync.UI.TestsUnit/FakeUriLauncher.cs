namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeUriLauncher : IUriLauncher
{
    public List<Uri> Launched { get; } = [];

    public Task<bool> LaunchAsync(Uri uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Launched.Add(uri);

        return Task.FromResult(true);
    }
}

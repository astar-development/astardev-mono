using AStarDev.CloudSync.UI;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeFolderPicker(string? chosen) : IFolderPicker
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(chosen);
    }
}

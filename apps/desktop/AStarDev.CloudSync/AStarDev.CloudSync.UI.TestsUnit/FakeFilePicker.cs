namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeFilePicker(string? chosen) : IFilePicker
{
    public Task<string?> PickSaveFileAsync(string suggestedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(chosen);
    }
}

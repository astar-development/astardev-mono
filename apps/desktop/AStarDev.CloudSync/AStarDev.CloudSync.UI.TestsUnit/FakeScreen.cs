using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeScreen(ScreenId id) : IScreen
{
    public string? LastTargetId { get; private set; }

    public ScreenId Id => id;

    public string Title => id.ToString();

    public string Glyph => string.Empty;

    public void OnNavigatedTo(string? targetId) => LastTargetId = targetId;
}

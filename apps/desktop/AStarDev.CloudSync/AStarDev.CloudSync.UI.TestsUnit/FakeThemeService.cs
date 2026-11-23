using AStarDev.CloudSync.UI.Theming;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeThemeService : IThemeService
{
    public string? Current { get; private set; }

    public IReadOnlyList<string> Available => ["Dark", "Light", "System"];

    public Result<Unit, ThemeError> Apply(string themeName)
    {
        Current = themeName;

        return new Ok<Unit>(default);
    }
}

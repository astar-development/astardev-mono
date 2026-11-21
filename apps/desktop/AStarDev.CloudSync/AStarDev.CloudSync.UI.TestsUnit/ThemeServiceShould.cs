using AStarDev.CloudSync.UI.Theming;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ThemeServiceShould
{
    [Fact]
    public void fail_when_no_application_is_running() => new ThemeService().Apply("Dark").Value.ShouldBeOfType<Fail<ThemeError>>().Error.Value.ShouldBeOfType<NoApplication>();

    [Fact]
    public void have_no_current_theme_until_one_is_applied() => new ThemeService().Current.ShouldBeNull();
}

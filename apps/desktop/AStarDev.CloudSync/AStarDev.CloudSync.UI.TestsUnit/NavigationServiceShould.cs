using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class NavigationServiceShould
{
    private static FakeScreen[] AllScreens() => Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)).ToArray();

    [Fact]
    public void land_on_the_dashboard()
    {
        var navigation = new NavigationService(AllScreens());

        navigation.Current.Id.ShouldBe(ScreenId.Dashboard);
        navigation.CurrentTargetId.ShouldBeNull();
    }

    [Fact]
    public void deep_link_to_a_screen_with_a_target()
    {
        var screens = AllScreens();
        var navigation = new NavigationService(screens);

        var result = navigation.Navigate(ScreenId.Conflicts, "conflict-7");

        result.Value.ShouldBeOfType<Ok<Unit>>();
        navigation.Current.Id.ShouldBe(ScreenId.Conflicts);
        navigation.CurrentTargetId.ShouldBe("conflict-7");
        screens.Single(screen => screen.Id == ScreenId.Conflicts).LastTargetId.ShouldBe("conflict-7");
    }

    [Fact]
    public void treat_a_blank_target_as_no_target()
    {
        var navigation = new NavigationService(AllScreens());

        _ = navigation.Navigate(ScreenId.Queue, " ");

        navigation.CurrentTargetId.ShouldBeNull();
    }

    [Fact]
    public void raise_navigated_after_each_successful_navigation()
    {
        var navigation = new NavigationService(AllScreens());
        var raised = 0;
        navigation.Navigated += (_, _) => raised++;

        _ = navigation.Navigate(ScreenId.History);

        raised.ShouldBe(1);
    }

    [Fact]
    public void fail_and_stay_put_for_a_screen_that_is_not_registered()
    {
        var navigation = new NavigationService(AllScreens().Where(screen => screen.Id != ScreenId.Logs));

        var result = navigation.Navigate(ScreenId.Logs);

        result.Value.ShouldBeOfType<Fail<NavigationError>>().Error.Value.ShouldBeOfType<ScreenNotRegistered>().Screen.ShouldBe(ScreenId.Logs);
        navigation.Current.Id.ShouldBe(ScreenId.Dashboard);
    }
}

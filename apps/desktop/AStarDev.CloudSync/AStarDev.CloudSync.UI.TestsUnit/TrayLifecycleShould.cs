using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class TrayLifecycleShould
{
    private sealed class FakeShell : IMainWindowHost, IApplicationExit
    {
        public int Shown { get; private set; }

        public int Hidden { get; private set; }

        public int Exits { get; private set; }

        public void Show() => Shown++;

        public void Hide() => Hidden++;

        public void Exit() => Exits++;
    }

    private static (TrayLifecycle Tray, FakeShell Shell, NavigationService Navigation) Create()
    {
        var shell = new FakeShell();
        var navigation = new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)));

        return (new TrayLifecycle(shell, navigation, shell), shell, navigation);
    }

    [Fact]
    public void hide_instead_of_exit_when_the_window_closes()
    {
        var (tray, shell, _) = Create();

        var cancelClose = tray.HandleWindowClosing();

        cancelClose.ShouldBeTrue();
        shell.Hidden.ShouldBe(1);
        shell.Exits.ShouldBe(0);
    }

    [Fact]
    public void exit_only_from_the_exit_command()
    {
        var (tray, shell, _) = Create();

        tray.OpenDashboard();
        tray.PauseSync();
        tray.ResumeSync();
        tray.OpenSettings();
        shell.Exits.ShouldBe(0);
        tray.Exit();
        var cancelClose = tray.HandleWindowClosing();

        shell.Exits.ShouldBe(1);
        cancelClose.ShouldBeFalse();
    }

    [Fact]
    public void show_the_window_and_deep_link_when_asked_to_open_a_screen_with_a_target()
    {
        var (tray, shell, navigation) = Create();

        var result = tray.Open(ScreenId.Conflicts, "c-1");

        result.Value.ShouldBeOfType<Ok<Unit>>();
        shell.Shown.ShouldBe(1);
        navigation.Current.Id.ShouldBe(ScreenId.Conflicts);
        navigation.CurrentTargetId.ShouldBe("c-1");
    }

    [Fact]
    public void open_the_dashboard_and_settings_from_the_menu()
    {
        var (tray, _, navigation) = Create();

        tray.OpenSettings();
        navigation.Current.Id.ShouldBe(ScreenId.Settings);
        tray.OpenDashboard();

        navigation.Current.Id.ShouldBe(ScreenId.Dashboard);
    }

    [Fact]
    public void raise_icon_state_changed_when_the_state_changes()
    {
        var (tray, _, _) = Create();
        var raised = 0;
        tray.IconStateChanged += (_, _) => raised++;

        tray.SetIconState(TrustLevel.Critical);

        tray.IconState.ShouldBe(TrustLevel.Critical);
        raised.ShouldBe(1);
    }
}

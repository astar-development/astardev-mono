using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class TrayConflictBadgeShould
{
    private sealed class FakeShell : IMainWindowHost, IApplicationExit
    {
        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Exit()
        {
        }
    }

    private static ConflictView Conflict() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ModifyModify", DateTimeOffset.UnixEpoch, "Open", "a.txt", null, null);

    private static (ConnectionViewModel Connection, TrayLifecycle Tray) Create(params ConflictView[] existing)
    {
        var shell = new FakeShell();
        var tray = new TrayLifecycle(shell, new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id))), shell);
        var connection = new ConnectionViewModel(() => new FakeIpcClient([], available: false), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        foreach (var conflict in existing) connection.Conflicts.Add(conflict);
        _ = new TrayConflictBadge(connection, tray);

        return (connection, tray);
    }

    [Fact]
    public void start_healthy_with_no_badge_when_nothing_waits()
    {
        var (_, tray) = Create();

        tray.ConflictCount.ShouldBe(0);
        tray.IconState.ShouldBe(TrustLevel.Healthy);
    }

    [Fact]
    public void show_the_conflicts_already_waiting()
    {
        var (_, tray) = Create(Conflict(), Conflict());

        tray.ConflictCount.ShouldBe(2);
        tray.IconState.ShouldBe(TrustLevel.Attention);
    }

    [Fact]
    public void follow_conflicts_as_they_are_detected_and_resolved()
    {
        var (connection, tray) = Create();
        var first = Conflict();

        connection.Conflicts.Add(first);
        connection.Conflicts.Add(Conflict());
        tray.ConflictCount.ShouldBe(2);
        connection.Conflicts.Remove(first);
        tray.ConflictCount.ShouldBe(1);
        connection.Conflicts.Clear();

        tray.ConflictCount.ShouldBe(0);
        tray.IconState.ShouldBe(TrustLevel.Healthy);
    }

    [Fact]
    public void raise_the_icon_changed_event_when_the_count_changes()
    {
        var (connection, tray) = Create();
        var raised = 0;
        tray.IconStateChanged += (_, _) => raised++;

        connection.Conflicts.Add(Conflict());

        raised.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void treat_a_negative_count_as_zero()
    {
        var (_, tray) = Create();

        tray.SetConflictCount(-3);

        tray.ConflictCount.ShouldBe(0);
    }

    [Fact]
    public void open_the_conflict_centre_from_the_tray()
    {
        var shell = new FakeShell();
        var navigation = new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)));
        var tray = new TrayLifecycle(shell, navigation, shell);

        tray.OpenConflicts();

        navigation.Current.Id.ShouldBe(ScreenId.Conflicts);
    }

    [Fact]
    public void draw_the_badge_only_when_asked()
    {
        const int cornerPixel = ((7 * TrayIconPixels.Size) + (TrayIconPixels.Size - 8)) * 4;

        var plain = TrayIconPixels.Render(TrustLevel.Attention);
        var badged = TrayIconPixels.Render(TrustLevel.Attention, badge: true);

        plain[cornerPixel + 3].ShouldBe((byte)0);
        badged[cornerPixel + 3].ShouldBe((byte)255);
        badged[cornerPixel + 2].ShouldBe((byte)0xF4);
    }
}

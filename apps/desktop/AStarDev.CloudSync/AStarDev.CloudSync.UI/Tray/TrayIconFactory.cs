using AStarDev.CloudSync.UI.Localisation;
using Avalonia.Controls;
using Avalonia.Platform;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>Builds the system tray icon and its menu over a <see cref="TrayLifecycle"/>.</summary>
internal static class TrayIconFactory
{
    private const string IconUri = "avares://AStarDev.CloudSync.UI/Assets/avalonia-logo.ico";

    /// <summary>Creates the icon with the menu Open Dashboard, Pause Sync, Resume Sync, Settings, Exit CloudSync. Clicking the icon opens the Dashboard.</summary>
    public static TrayIcon Create(TrayLifecycle lifecycle, ILocalisationService localisation)
    {
        var menu = new NativeMenu();
        menu.Items.Add(Item(localisation.Get("Tray.OpenDashboard"), lifecycle.OpenDashboard));
        menu.Items.Add(Item(localisation.Get("Tray.PauseSync"), lifecycle.PauseSync));
        menu.Items.Add(Item(localisation.Get("Tray.ResumeSync"), lifecycle.ResumeSync));
        menu.Items.Add(Item(localisation.Get("Tray.Settings"), lifecycle.OpenSettings));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(localisation.Get("Tray.Exit"), lifecycle.Exit));
        var icon = new TrayIcon { Icon = new WindowIcon(AssetLoader.Open(new Uri(IconUri))), ToolTipText = localisation.Get("Tray.ToolTip"), Menu = menu };
        icon.Clicked += (_, _) => lifecycle.OpenDashboard();

        return icon;
    }

    private static NativeMenuItem Item(string header, Action onClick)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => onClick();

        return item;
    }
}

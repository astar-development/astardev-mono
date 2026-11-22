using System.Runtime.InteropServices;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>Builds the system tray icon and its menu over a <see cref="TrayLifecycle"/>.</summary>
internal static class TrayIconFactory
{
    /// <summary>
    /// Creates the icon with the menu Open Dashboard, Conflicts (with the count), Pause Sync, Resume Sync, Settings, Exit CloudSync. Clicking the icon opens the Dashboard.
    /// The icon shape and tooltip follow <see cref="TrayLifecycle.IconState"/>, and a badge, the tooltip and the Conflicts item follow <see cref="TrayLifecycle.ConflictCount"/>; all redraw whenever either changes.
    /// </summary>
    public static TrayIcon Create(TrayLifecycle lifecycle, ILocalisationService localisation)
    {
        var menu = new NativeMenu();
        menu.Items.Add(Item(localisation.Get("Tray.OpenDashboard"), lifecycle.OpenDashboard));
        var conflicts = Item(localisation.Get("Tray.Conflicts"), lifecycle.OpenConflicts);
        menu.Items.Add(conflicts);
        menu.Items.Add(Item(localisation.Get("Tray.PauseSync"), lifecycle.PauseSync));
        menu.Items.Add(Item(localisation.Get("Tray.ResumeSync"), lifecycle.ResumeSync));
        menu.Items.Add(Item(localisation.Get("Tray.Settings"), lifecycle.OpenSettings));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(localisation.Get("Tray.Exit"), lifecycle.Exit));
        var icon = new TrayIcon { Menu = menu };
        Apply(icon, conflicts, lifecycle, localisation);
        icon.Clicked += (_, _) => lifecycle.OpenDashboard();
        lifecycle.IconStateChanged += (_, _) => Apply(icon, conflicts, lifecycle, localisation);

        return icon;
    }

    private static void Apply(TrayIcon icon, NativeMenuItem conflicts, TrayLifecycle lifecycle, ILocalisationService localisation)
    {
        var level = lifecycle.IconState;
        var count = lifecycle.ConflictCount;
        var bitmap = new WriteableBitmap(new PixelSize(TrayIconPixels.Size, TrayIconPixels.Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock()) Marshal.Copy(TrayIconPixels.Render(level, count > 0), 0, buffer.Address, TrayIconPixels.Size * TrayIconPixels.Size * 4);
        icon.Icon = new WindowIcon(bitmap);
        icon.ToolTipText = TrayState.ToolTipFor(level, count, localisation);
        conflicts.Header = count > 0 ? localisation.Format("Tray.Conflicts.Count", ("count", count)) : localisation.Get("Tray.Conflicts");
    }

    private static NativeMenuItem Item(string header, Action onClick)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => onClick();

        return item;
    }
}

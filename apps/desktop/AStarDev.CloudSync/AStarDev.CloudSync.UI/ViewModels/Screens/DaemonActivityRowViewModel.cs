using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One daemon activity as text. Every number comes from the daemon's <see cref="ActivityView"/>; the only work done here is wording and formatting.</summary>
internal sealed class DaemonActivityRowViewModel
{
    /// <summary>Words the activity for display.</summary>
    /// <param name="activity">What the daemon reported.</param>
    /// <param name="localisation">Supplies every text.</param>
    public DaemonActivityRowViewModel(ActivityView activity, ILocalisationService localisation)
    {
        Title = localisation.Format("Activity.Daemon.Row", ("state", localisation.Get($"Activity.Daemon.State.{activity.State}")), ("name", activity.Name));
        ProgressText = activity.ItemsTotal is { } total && activity.Percent is { } percent
            ? localisation.Format("Activity.Daemon.Progress", ("done", activity.ItemsDone), ("total", total), ("percent", percent))
            : localisation.Format("Activity.Daemon.ProgressNoTotal", ("done", activity.ItemsDone));
        Percent = activity.Percent ?? 0;
        HasPercent = activity.Percent is not null;
        EtaText = activity.EtaSeconds is { } seconds ? Eta(seconds, localisation) : null;
        RetryText = activity.RetryInSeconds is { } retry ? localisation.Format("Activity.Daemon.Retry", ("seconds", retry)) : null;
        ActionRequiredText = activity.ActionRequired ? localisation.Get("Activity.Daemon.ActionRequired") : null;
    }

    /// <summary>The state and the job name, for example "Initial indexing: OneDrive".</summary>
    public string Title { get; }

    /// <summary>The count, with the total and percentage when the daemon gave them.</summary>
    public string ProgressText { get; }

    /// <summary>The daemon's percentage, 0 when it gave none; see <see cref="HasPercent"/>.</summary>
    public double Percent { get; }

    /// <summary>Whether the daemon gave a percentage, so a progress bar means something.</summary>
    public bool HasPercent { get; }

    /// <summary>The daemon's time estimate in words; <see langword="null"/> when it gave none.</summary>
    public string? EtaText { get; }

    /// <summary>The wait before the next try; <see langword="null"/> unless the daemon is waiting.</summary>
    public string? RetryText { get; }

    /// <summary>Why the user must act; <see langword="null"/> when nothing is needed.</summary>
    public string? ActionRequiredText { get; }

    private static string Eta(int seconds, ILocalisationService localisation)
        => seconds < 60 ? localisation.Get("Activity.Daemon.EtaSoon") : localisation.Format("Activity.Daemon.Eta", ("minutes", (long)Math.Round(seconds / 60d)));
}

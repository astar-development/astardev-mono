using AStarDev.CloudSync.UI.Conflicts;
using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>A conflict prepared for display: list row and detail comparison share it. Every text is localised.</summary>
internal sealed class ConflictRowViewModel : ViewModelBase
{
    private const long BytesPerKib = 1024;

    private readonly ILocalisationService localisation;

    private bool isSelected;

    /// <summary>Creates the row for <paramref name="item"/>.</summary>
    public ConflictRowViewModel(ConflictItem item, ILocalisationService localisation)
    {
        Item = item;
        this.localisation = localisation;
        Status = new StatusIndicatorViewModel(item.Status == ConflictStatus.Deferred ? TrustLevel.Degraded : TrustLevel.Attention, localisation.Get($"Conflicts.Status.{item.Status}"));
        AgeText = item.Age.TotalDays >= 1 ? localisation.Format("Conflicts.Age.Days", ("count", (int)item.Age.TotalDays)) : localisation.Format("Conflicts.Age.Hours", ("count", (int)item.Age.TotalHours));
        ReasonText = localisation.Get($"Conflicts.Reason.{item.Reason}");
    }

    /// <summary>The conflict shown.</summary>
    public ConflictItem Item { get; }

    /// <summary>The item's file name.</summary>
    public string Name => Item.Name;

    /// <summary>Status as colour, icon and text.</summary>
    public StatusIndicatorViewModel Status { get; }

    /// <summary>How long the conflict has been open, in whole days or, under a day, whole hours.</summary>
    public string AgeText { get; }

    /// <summary>Why the conflict exists, in words a user can act on.</summary>
    public string ReasonText { get; }

    /// <summary>The local path.</summary>
    public string LocalPath => Item.Local.Path;

    /// <summary>The remote path.</summary>
    public string RemotePath => Item.Remote.Path;

    /// <summary>When the local version last changed.</summary>
    public string LocalModifiedText => Modified(Item.Local);

    /// <summary>When the remote version last changed.</summary>
    public string RemoteModifiedText => Modified(Item.Remote);

    /// <summary>The local size, or the word for deleted when the local version is gone.</summary>
    public string LocalSizeText => Size(Item.Local);

    /// <summary>The remote size, or the word for deleted when the remote version is gone.</summary>
    public string RemoteSizeText => Size(Item.Remote);

    /// <summary>Whether the row is ticked for a bulk action.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    private string Modified(ConflictVersion version) => localisation.Format("Conflicts.Modified", ("value", version.Modified));

    private string Size(ConflictVersion version) => version.SizeBytes == 0 ? localisation.Get("Conflicts.Size.Missing") : localisation.Format("Conflicts.Size.Kib", ("kib", (version.SizeBytes + BytesPerKib - 1) / BytesPerKib));
}

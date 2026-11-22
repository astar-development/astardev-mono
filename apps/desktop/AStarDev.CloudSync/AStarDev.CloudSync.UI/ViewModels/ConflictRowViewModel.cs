using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>A conflict prepared for display: list row and detail comparison share it. Every text is localised.</summary>
internal sealed class ConflictRowViewModel : ViewModelBase
{
    private const long BytesPerKib = 1024;
    private const string DeferredStatus = "Deferred";
    private static readonly HashSet<string> KnownTypes = ["CreateCreate", "ModifyModify", "RenameRename", "KeepDelete"];

    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;

    private bool isSelected;

    /// <summary>Creates the row for <paramref name="view"/>.</summary>
    /// <param name="view">What the daemon reported.</param>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="now">The current time, from which the age is measured.</param>
    public ConflictRowViewModel(ConflictView view, ILocalisationService localisation, ICultureService culture, DateTimeOffset now)
    {
        View = view;
        this.localisation = localisation;
        this.culture = culture;
        IsDeferred = view.Status == DeferredStatus;
        var type = KnownTypes.Contains(view.Type) ? view.Type : "Unknown";
        var status = IsDeferred ? DeferredStatus : "Unresolved";
        Status = new StatusIndicatorViewModel(IsDeferred ? TrustLevel.Degraded : TrustLevel.Attention, localisation.Get($"Conflicts.Status.{status}"));
        var age = now - view.DetectedUtc;
        AgeText = age.TotalDays >= 1 ? localisation.Format("Conflicts.Age.Days", ("count", (int)age.TotalDays)) : localisation.Format("Conflicts.Age.Hours", ("count", Math.Max(0, (int)age.TotalHours)));
        DetectedText = localisation.Format("Conflicts.Detected", ("date", culture.FormatDate(view.DetectedUtc)), ("time", culture.FormatTime(view.DetectedUtc)));
        TypeText = localisation.Get($"Conflicts.Type.{type}");
        ReasonText = localisation.Get(ReasonKey(type, view));
    }

    /// <summary>The conflict as the daemon reported it.</summary>
    public ConflictView View { get; }

    /// <summary>The daemon's identity for the conflict, as sent back in a resolve command and as a deep link names it.</summary>
    public Guid Id => View.ConflictId;

    /// <summary>The item's file name: the last part of its path, or the whole path when it has no folder.</summary>
    public string Name => View.Path[(View.Path.LastIndexOf('/') + 1)..];

    /// <summary>The item's path relative to the synchronised root.</summary>
    public string PathText => View.Path;

    /// <summary>True for a conflict the user postponed. It is still unresolved and can be resolved.</summary>
    public bool IsDeferred { get; }

    /// <summary>Status as colour, icon and text.</summary>
    public StatusIndicatorViewModel Status { get; }

    /// <summary>How long the conflict has been open, in whole days or, under a day, whole hours.</summary>
    public string AgeText { get; }

    /// <summary>When the daemon detected the conflict.</summary>
    public string DetectedText { get; }

    /// <summary>The kind of conflict in a few words, for example "Concurrent modification".</summary>
    public string TypeText { get; }

    /// <summary>What happened and why CloudSync could not decide, in words a user can act on.</summary>
    public string ReasonText { get; }

    /// <summary>The local path, or the word for not present when the local file is gone.</summary>
    public string LocalPath => Side(View.Local, side => side.Path);

    /// <summary>The remote path, or the word for not present when the remote file is gone.</summary>
    public string RemotePath => Side(View.Remote, side => side.Path);

    /// <summary>When the local version last changed, or the word for not present.</summary>
    public string LocalModifiedText => Side(View.Local, Modified);

    /// <summary>When the remote version last changed, or the word for not present.</summary>
    public string RemoteModifiedText => Side(View.Remote, Modified);

    /// <summary>The local size in KiB, or the word for not present.</summary>
    public string LocalSizeText => Side(View.Local, Size);

    /// <summary>The remote size in KiB, or the word for not present.</summary>
    public string RemoteSizeText => Side(View.Remote, Size);

    /// <summary>Whether the row is ticked for a bulk action.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    private static string ReasonKey(string type, ConflictView view) => type == "KeepDelete"
        ? view.Local is null ? "Conflicts.Reason.KeepDelete.Local" : view.Remote is null ? "Conflicts.Reason.KeepDelete.Remote" : "Conflicts.Reason.KeepDelete"
        : $"Conflicts.Reason.{type}";

    private string Side(ConflictSideView? side, Func<ConflictSideView, string> text) => side is null ? localisation.Get("Conflicts.Side.Missing") : text(side);

    private string Modified(ConflictSideView side) => localisation.Format("Conflicts.Modified", ("date", culture.FormatDate(side.ModifiedUtc)), ("time", culture.FormatTime(side.ModifiedUtc)));

    private string Size(ConflictSideView side) => localisation.Format("Conflicts.Size.Kib", ("kib", (side.SizeBytes + BytesPerKib - 1) / BytesPerKib));
}

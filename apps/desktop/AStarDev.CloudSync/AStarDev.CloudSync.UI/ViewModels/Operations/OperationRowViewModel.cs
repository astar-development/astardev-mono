using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>One operation as a list row: localised type, status shown as colour, icon and text, and culture-formatted time.</summary>
internal sealed class OperationRowViewModel : ViewModelBase
{
    /// <summary>Creates a row for <paramref name="entry"/>, formatting dates and numbers with the culture service and texts with localisation.</summary>
    public OperationRowViewModel(OperationEntry entry, ILocalisationService localisation, ICultureService culture)
    {
        Entry = entry;
        TypeText = localisation.Get($"Operations.Type.{entry.Type}");
        Status = new StatusIndicatorViewModel(OperationStatusLevel.Of(entry.Status), localisation.Get($"Operations.Status.{entry.Status}"));
        Date = culture.FormatDate(entry.Timestamp);
        Time = culture.FormatTime(entry.Timestamp);
        SizeText = culture.FormatNumber(entry.SizeBytes);
    }

    /// <summary>The raw operation behind this row.</summary>
    public OperationEntry Entry { get; }

    /// <summary>Localised name of the operation type.</summary>
    public string TypeText { get; }

    /// <summary>The status as colour, icon and text together, so the outcome never rests on colour alone.</summary>
    public StatusIndicatorViewModel Status { get; }

    /// <summary>The recorded date in the short pattern of the active culture.</summary>
    public string Date { get; }

    /// <summary>The recorded time of day in the short pattern of the active culture.</summary>
    public string Time { get; }

    /// <summary>The item size in bytes with the digit grouping of the active culture.</summary>
    public string SizeText { get; }
}

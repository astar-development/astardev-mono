namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>The details panel of a list: shows one operation beside the list, so the user inspects it without leaving the screen.</summary>
internal sealed class OperationDetailsViewModel : ViewModelBase
{
    private OperationRowViewModel? row;

    /// <summary>The operation being inspected, or <see langword="null"/> when the panel is closed. Selecting a row in the list sets it.</summary>
    public OperationRowViewModel? Row
    {
        get => row;
        set
        {
            if (SetProperty(ref row, value)) OnPropertyChanged(nameof(IsOpen));
        }
    }

    /// <summary>True while an operation is shown.</summary>
    public bool IsOpen => row is not null;

    /// <summary>Shows <paramref name="operation"/> in the panel. Never navigates: the screen and its filters stay as they are.</summary>
    public void Open(OperationRowViewModel operation) => Row = operation;

    /// <summary>Closes the panel. Safe to call when already closed.</summary>
    public void Close() => Row = null;
}

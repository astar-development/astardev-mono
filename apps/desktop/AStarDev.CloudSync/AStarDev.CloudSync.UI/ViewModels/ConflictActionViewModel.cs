using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One resolution button of the Conflicts screen.</summary>
internal sealed class ConflictActionViewModel(ConflictChoice choice, string label) : ViewModelBase
{
    private bool isEnabled;

    /// <summary>The decision the button requests.</summary>
    public ConflictChoice Choice => choice;

    /// <summary>Localised button text. It also says when the choice is the safest one, so the promotion never depends on colour.</summary>
    public string Label => label;

    /// <summary>False while no conflict is focused or ticked, so there is nothing to decide for.</summary>
    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }
}

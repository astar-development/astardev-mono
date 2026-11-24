namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One category as it is offered in the pickers and the edit form.</summary>
/// <param name="Id">The category's identity, as the daemon names it.</param>
/// <param name="Name">The name shown to the user.</param>
internal sealed record CategoryRowViewModel(Guid Id, string Name)
{
    /// <summary>Returns the name, so a picker shows it.</summary>
    public override string ToString() => Name;
}

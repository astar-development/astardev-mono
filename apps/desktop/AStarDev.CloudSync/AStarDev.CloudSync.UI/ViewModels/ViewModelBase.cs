using CommunityToolkit.Mvvm.ComponentModel;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Base for every view model: supplies change notification and marks the type as one <see cref="ViewLocator"/> resolves a view for.</summary>
public abstract class ViewModelBase : ObservableObject
{
}

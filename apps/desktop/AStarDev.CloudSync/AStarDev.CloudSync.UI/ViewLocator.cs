using System;
using System.Diagnostics.CodeAnalysis;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.Views;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AStarDev.CloudSync.UI;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    /// <summary>Creates the view for <paramref name="param"/> by convention: its full type name with <c>ViewModel</c> replaced by <c>View</c>.</summary>
    /// <returns>A new view; a <see cref="TextBlock"/> naming the missing type when no view matches; <see langword="null"/> for a <see langword="null"/> data context.</returns>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>Claims only <see cref="ViewModelBase"/> instances, so any other data context falls through to other templates.</summary>
    public bool Match(object? data) => data is ViewModelBase;
}

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One log entry as text, ready to show as it is.</summary>
/// <param name="Header">Date, time, level and source on one line.</param>
/// <param name="Message">The message as the daemon stored it.</param>
internal sealed record LogRowViewModel(string Header, string Message);

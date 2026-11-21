namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>A placeholder entry point in Settings that is not built yet. It says what the change would do and what happens next, so nothing impactful is hidden.</summary>
/// <param name="Heading">Localised name of the action.</param>
/// <param name="Consequences">What the change would affect, in plain words.</param>
/// <param name="WhatHappensNext">What the user can expect, including that the guided change is not available yet.</param>
internal sealed record SettingsEntryViewModel(string Heading, string Consequences, string WhatHappensNext);

namespace AStarDev.CloudSync.IPC;

/// <summary>The user settings the daemon holds, answering <see cref="GetSettings"/>.</summary>
/// <param name="Values">Every known setting by name, each with its saved value or its default, so no setting is ever missing.</param>
public sealed record SettingsView(IReadOnlyDictionary<string, string> Values);

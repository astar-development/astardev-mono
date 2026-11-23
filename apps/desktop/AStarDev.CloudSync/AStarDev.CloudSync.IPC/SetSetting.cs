namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to save one setting. Answered with <see cref="CommandOutcome.Accepted"/> once stored, or <see cref="CommandOutcome.Rejected"/>
/// with a message the UI can show as it is (unknown setting, value not allowed, database failure).
/// </summary>
/// <param name="Name">The setting to change.</param>
/// <param name="Value">The new value, in the form the setting documents.</param>
public sealed record SetSetting(string Name, string Value) : ICommand;

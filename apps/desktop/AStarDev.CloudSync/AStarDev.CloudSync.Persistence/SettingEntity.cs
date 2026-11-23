namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of one user setting. Only settings that were changed have a row.</summary>
internal sealed class SettingEntity
{
    /// <summary>Primary key: one of the names in <c>SettingNames</c>.</summary>
    public required string Name { get; set; }

    /// <summary>The saved value, already checked against the setting's rules.</summary>
    public required string Value { get; set; }
}

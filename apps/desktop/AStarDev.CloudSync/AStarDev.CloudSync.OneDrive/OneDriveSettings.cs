namespace AStarDev.CloudSync.OneDrive;

/// <summary>The <c>OneDrive</c> configuration section.</summary>
internal sealed class OneDriveSettings
{
    /// <summary>Name of the configuration section that binds to this type.</summary>
    public const string ConfigurationSection = "OneDrive";

    /// <summary>Client id of the app registration used for sign-in. Missing or blank makes connecting fail with a message, not the daemon fail to start.</summary>
    public string? ClientId { get; set; }
}

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>The steps of the add-provider journey, in the order the user meets them.</summary>
internal enum AddProviderStep
{
    /// <summary>Choose which provider to add.</summary>
    SelectProvider,

    /// <summary>Sign in at the provider.</summary>
    Authenticate,

    /// <summary>Review the access the provider granted.</summary>
    GrantPermissions,

    /// <summary>Confirm the account can be read.</summary>
    VerifyAccess,

    /// <summary>Choose where files are synchronised to.</summary>
    ChooseSyncRoot,

    /// <summary>Check the configuration before it starts.</summary>
    ReviewConfiguration,

    /// <summary>Start syncing.</summary>
    Activate
}

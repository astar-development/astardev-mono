namespace AStarDev.CloudSync.UI.ViewModels.Onboarding;

/// <summary>The steps of the first-run flow in the order doc 20 defines. The Dashboard that follows is a screen, not a step.</summary>
internal enum OnboardingStep
{
    /// <summary>Introduces CloudSync and what setup will and will not do.</summary>
    Welcome,

    /// <summary>The user signs in to a provider.</summary>
    ConnectProvider,

    /// <summary>The user ticks the folders to synchronise and picks the local folder.</summary>
    SelectScopes,

    /// <summary>The summary the user must confirm before anything is synchronised.</summary>
    Review,

    /// <summary>The first synchronisation, shown with the progress the daemon reports.</summary>
    InitialSync
}

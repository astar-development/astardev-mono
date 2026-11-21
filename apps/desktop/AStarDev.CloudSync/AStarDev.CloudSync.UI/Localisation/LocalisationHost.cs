namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>Hands the composed <see cref="ILocalisationService"/> to XAML, which cannot take constructor injection.</summary>
internal static class LocalisationHost
{
    /// <summary>The service <see cref="LocExtension"/> reads. <see langword="null"/> until the application sets it from its composed host, which makes every XAML text show as its key.</summary>
    public static ILocalisationService? Service { get; set; }
}

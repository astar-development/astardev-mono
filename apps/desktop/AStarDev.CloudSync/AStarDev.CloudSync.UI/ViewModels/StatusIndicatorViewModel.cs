using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One status shown as colour, icon and text together. Reuse it for any health badge; <see cref="Views.StatusIndicatorView"/> draws it.</summary>
public sealed class StatusIndicatorViewModel : ViewModelBase
{
    /// <summary>Creates an indicator.</summary>
    /// <param name="level">Chooses the colour and icon.</param>
    /// <param name="text">The words shown beside the icon. Blank text becomes the name of <paramref name="level"/>, so the text is never empty.</param>
    public StatusIndicatorViewModel(TrustLevel level, string text)
    {
        Level = level;
        Text = string.IsNullOrWhiteSpace(text) ? level.ToString() : text;
    }

    /// <summary>The trust level this indicator shows.</summary>
    public TrustLevel Level { get; }

    /// <summary>The words shown beside the icon. Never blank.</summary>
    public string Text { get; }

    /// <summary>The theme resource key of the brush, for example <c>Trust.Healthy.Brush</c>. The active theme decides the actual colour.</summary>
    public string ColourKey => $"Trust.{Level}.Brush";

    /// <summary>The resource key of the icon geometry, for example <c>Icon.Trust.Healthy</c>. Each level has a different shape.</summary>
    public string IconKey => $"Icon.Trust.{Level}";

    internal static StatusIndicatorViewModel For(TrustLevel level, ILocalisationService localisation) => new(level, localisation.Get($"Common.Trust.{level}"));
}

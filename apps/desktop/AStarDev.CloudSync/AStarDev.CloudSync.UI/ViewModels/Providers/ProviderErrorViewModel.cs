using AStarDev.CloudSync.UI.Localisation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>An error with what to do next: the message, retry guidance, a diagnostics link and a correlation id placeholder.</summary>
/// <param name="message">What went wrong, worded for the user.</param>
/// <param name="localisation">Supplies the guidance texts.</param>
/// <param name="openDiagnostics">Takes the user to the diagnostics screen.</param>
internal sealed partial class ProviderErrorViewModel(string message, ILocalisationService localisation, Action openDiagnostics) : ViewModelBase
{
    /// <summary>What went wrong, worded for the user.</summary>
    public string Message => message;

    /// <summary>What the user can try, for example retrying after checking the daemon.</summary>
    public string RetryGuidance => localisation.Get("Providers.Error.Retry");

    /// <summary>Placeholder for the correlation id, worded as not available yet because the daemon does not send one.</summary>
    public string CorrelationText => localisation.Get("Providers.Error.Correlation");

    /// <summary>Opens the diagnostics screen. Also exposed to the view as <c>OpenDiagnosticsCommand</c>.</summary>
    [RelayCommand]
    public void OpenDiagnostics() => openDiagnostics();
}

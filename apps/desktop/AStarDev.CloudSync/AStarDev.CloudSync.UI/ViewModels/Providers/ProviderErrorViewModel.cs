using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>An error with what to do next: the message, retry guidance, a diagnostics link and the correlation id of the daemon trace.</summary>
/// <param name="message">What went wrong, worded for the user.</param>
/// <param name="traceId">The daemon trace that handled the failed request, when the daemon sent one.</param>
/// <param name="localisation">Supplies the guidance texts.</param>
/// <param name="openDiagnostics">Takes the user to the diagnostics screen.</param>
internal sealed partial class ProviderErrorViewModel(string message, Option<string> traceId, ILocalisationService localisation, Action openDiagnostics) : ViewModelBase
{
    /// <summary>What went wrong, worded for the user.</summary>
    public string Message => message;

    /// <summary>What the user can try, for example retrying after checking the daemon.</summary>
    public string RetryGuidance => localisation.Get("Providers.Error.Retry");

    /// <summary>The daemon trace id to quote when reporting the failure, or text saying the daemon sent none.</summary>
    public string CorrelationText => traceId.Match(id => localisation.Format("Providers.Error.Correlation", ("id", id)), () => localisation.Get("Providers.Error.CorrelationNone"));

    /// <summary>Opens the diagnostics screen. Also exposed to the view as <c>OpenDiagnosticsCommand</c>.</summary>
    [RelayCommand]
    public void OpenDiagnostics() => openDiagnostics();
}

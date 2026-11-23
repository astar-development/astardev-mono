using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>An error with what to do next: the message, retry guidance, a diagnostics link, the error code with its help link, and the correlation id of the daemon trace.</summary>
/// <param name="message">What went wrong, worded for the user.</param>
/// <param name="traceId">The daemon trace that handled the failed request, when the daemon sent one.</param>
/// <param name="code">The code of the failure; <c>default</c> when it has none, which hides the code and the help link.</param>
/// <param name="launcher">Opens the help link in the browser.</param>
/// <param name="localisation">Supplies the guidance texts.</param>
/// <param name="openDiagnostics">Takes the user to the diagnostics screen.</param>
internal sealed partial class ProviderErrorViewModel(string message, Option<string> traceId, ErrorCode code, IUriLauncher launcher, ILocalisationService localisation, Action openDiagnostics) : ViewModelBase
{
    /// <summary>What went wrong, worded for the user.</summary>
    public string Message => message;

    /// <summary>What the user can try, for example retrying after checking the daemon.</summary>
    public string RetryGuidance => localisation.Get("Providers.Error.Retry");

    /// <summary>The daemon trace id to quote when reporting the failure, or text saying the daemon sent none.</summary>
    public string CorrelationText => traceId.Match(id => localisation.Format("Providers.Error.Correlation", ("id", id)), () => localisation.Get("Providers.Error.CorrelationNone"));

    /// <summary>True when the failure has a code, and so a help link to show and open.</summary>
    public bool HasLink => !string.IsNullOrEmpty(code.Value);

    /// <summary>The error code to quote when reporting the failure, or empty when it has none.</summary>
    public string CodeText => HasLink ? localisation.Format("Providers.Error.Code", ("code", code.Value)) : string.Empty;

    /// <summary>The help page for the error code, or empty when it has none.</summary>
    public string HelpLink => HasLink ? ErrorLinks.For(code) : string.Empty;

    /// <summary>Opens <see cref="HelpLink"/> in the browser. Does nothing when there is no code, or when no browser could be started. Also exposed to the view as <c>OpenLinkCommand</c>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the request.</param>
    [RelayCommand]
    public async Task OpenLinkAsync(CancellationToken cancellationToken)
    {
        if (!HasLink) return;

        await launcher.LaunchAsync(new Uri(HelpLink), cancellationToken);
    }

    /// <summary>Opens the diagnostics screen. Also exposed to the view as <c>OpenDiagnosticsCommand</c>.</summary>
    [RelayCommand]
    public void OpenDiagnostics() => openDiagnostics();
}

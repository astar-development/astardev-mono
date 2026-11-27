using System.Diagnostics;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Shows notifications on Linux desktops by running <c>notify-send</c>, which talks to the freedesktop notification service. Clicking a notification prints its default action, which the result reports as activated. Other platforms report <see cref="NotifierUnavailable"/>.</summary>
/// <param name="localisation">Supplies the application name and body text of each notification.</param>
internal sealed class NotifySendNotifier(ILocalisationService localisation) : IDesktopNotifier
{
    private const string Program = "notify-send";
    private const string ActivateAction = "default";

    /// <inheritdoc/>
    public async Task<Result<bool, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsLinux()) return new Fail<DesktopNotifierError>(new NotifierUnavailable("Desktop notifications are only supported on Linux."));

        var run = await Try.RunAsync(token => RunAsync(StartInfo(notification), token), cancellationToken);

        return run.ToResult(exception => (DesktopNotifierError)new NotifierUnavailable(exception.Message)) switch
        {
            Ok<(int ExitCode, string Output)> { Value.ExitCode: 0 } ok => new Ok<bool>(ok.Value.Output.Trim() == ActivateAction),
            Ok<(int ExitCode, string Output)> failed => new Fail<DesktopNotifierError>(new NotifierFailed($"{Program} exited with code {failed.Value.ExitCode}.")),
            Fail<DesktopNotifierError> fail => fail
        };
    }

    internal ProcessStartInfo StartInfo(Notification notification)
    {
        var start = new ProcessStartInfo(Program) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add($"--app-name={localisation.Get("Tray.ToolTip")}");
        start.ArgumentList.Add($"--action={ActivateAction}={localisation.Get("Notifications.OpenAction")}");
        start.ArgumentList.Add($"--urgency={UrgencyOf(notification.Category)}");
        start.ArgumentList.Add(notification.Title);
        start.ArgumentList.Add(localisation.Format("Notifications.Body", ("message", notification.Message), ("nextStep", notification.NextStep)));

        return start;
    }

    private static string UrgencyOf(NotificationCategory category) => category is NotificationCategory.Error or NotificationCategory.Critical ? "critical" : "normal";

    private static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{Program} did not start.").WithHelpLink(UiErrorCodes.NotifierDidNotStart);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, output);
    }
}

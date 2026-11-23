using System.Diagnostics;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Shows notifications on Linux desktops by running <c>notify-send</c>, which talks to the freedesktop notification service. Other platforms report <see cref="NotifierUnavailable"/>.</summary>
/// <param name="localisation">Supplies the application name and body text of each notification.</param>
internal sealed class NotifySendNotifier(ILocalisationService localisation) : IDesktopNotifier
{
    private const string Program = "notify-send";

    /// <inheritdoc/>
    public async Task<Result<Unit, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsLinux()) return new Fail<DesktopNotifierError>(new NotifierUnavailable("Desktop notifications are only supported on Linux."));

        var exit = await Try.RunAsync(token => RunAsync(StartInfo(notification), token), cancellationToken);

        return exit.ToResult(exception => (DesktopNotifierError)new NotifierUnavailable(exception.Message)) switch
        {
            Ok<int> { Value: 0 } => new Ok<Unit>(default),
            Ok<int> failed => new Fail<DesktopNotifierError>(new NotifierFailed($"{Program} exited with code {failed.Value}.")),
            Fail<DesktopNotifierError> fail => fail
        };
    }

    internal ProcessStartInfo StartInfo(Notification notification)
    {
        var start = new ProcessStartInfo(Program) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add($"--app-name={localisation.Get("Tray.ToolTip")}");
        start.ArgumentList.Add($"--urgency={UrgencyOf(notification.Category)}");
        start.ArgumentList.Add(notification.Title);
        start.ArgumentList.Add(localisation.Format("Notifications.Body", ("message", notification.Message), ("nextStep", notification.NextStep)));

        return start;
    }

    private static string UrgencyOf(NotificationCategory category) => category is NotificationCategory.Error or NotificationCategory.Critical ? "critical" : "normal";

    private static async Task<int> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{Program} did not start.").WithHelpLink(UiErrorCodes.NotifierDidNotStart);
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode;
    }
}

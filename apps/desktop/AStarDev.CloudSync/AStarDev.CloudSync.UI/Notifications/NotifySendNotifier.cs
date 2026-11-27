using System.Diagnostics;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Shows notifications on Linux desktops by running <c>notify-send</c>, which talks to the freedesktop notification service. Clicking a notification prints its default action, which the result reports as activated. A notification not clicked within ten seconds has its process killed, which reports as not activated and frees the bus connection it holds. Other platforms report <see cref="NotifierUnavailable"/>.</summary>
/// <param name="localisation">Supplies the application name and body text of each notification.</param>
internal sealed class NotifySendNotifier(ILocalisationService localisation) : IDesktopNotifier
{
    private const string Program = "notify-send";
    private const string ActivateAction = "default";
    private static readonly TimeSpan ClickTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public async Task<Result<bool, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsLinux()) return new Fail<DesktopNotifierError>(new NotifierUnavailable("Desktop notifications are only supported on Linux."));

        var run = await Try.RunAsync(token => RunAsync(StartInfo(notification), ClickTimeout, token), cancellationToken);

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

    /// <summary>Runs the process and waits for it to exit, killing it when it outlives <paramref name="timeout"/> or the caller cancels.</summary>
    /// <param name="start">The process to run, with standard output redirected.</param>
    /// <param name="timeout">How long the process may run before it is killed. A notification nobody clicks or dismisses never exits on its own.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process.</param>
    /// <returns>The exit code and standard output, or exit code 0 with empty output when the process was killed at the timeout, which reads as not clicked. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    internal static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo start, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{Program} did not start.").WithHelpLink(UiErrorCodes.NotifierDidNotStart);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var exited = process.WaitForExitAsync(cancellationToken);

        var finished = await Task.WhenAny(exited, Task.Delay(Timeout.Infinite, deadline.Token));
        if (finished == exited && exited.IsCompletedSuccessfully) return (process.ExitCode, await output);

        _ = Try.Run(() => KillTree(process));
        cancellationToken.ThrowIfCancellationRequested();

        return (0, string.Empty);
    }

    private static bool KillTree(Process process)
    {
        process.Kill(true);

        return true;
    }
}

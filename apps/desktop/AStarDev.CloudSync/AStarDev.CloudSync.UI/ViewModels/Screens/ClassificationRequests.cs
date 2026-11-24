using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>Sends one command to the daemon on its own connection.</summary>
internal static class ClassificationRequests
{
    /// <summary>Connects, sends <paramref name="command"/> and waits for the answer.</summary>
    /// <param name="createClient">Creates a fresh client; one client is one connection.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="fallback">The text to show when the daemon declined without giving a reason.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Success when the daemon accepted, otherwise why not, worded for the user.</returns>
    public static async Task<Result<Unit, ConnectFailure>> SendAsync(Func<IIpcClient> createClient, ICommand command, string fallback, CancellationToken cancellationToken)
    {
        await using var client = createClient();
        if ((await client.ConnectAsync(cancellationToken)).Value is Fail<IpcError> unreachable) return new Fail<ConnectFailure>(ConnectFailure.From(unreachable.Error));

        return await client.SendAsync(command, cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => new Ok<Unit>(default),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(ConnectFailure.Rejected(rejected.Value, fallback)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };
    }
}

using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Asks the user where to save, fetches an export from the daemon and writes it there. Shared by the History and Audit screens.</summary>
/// <param name="createClient">Creates a fresh client per export; one client is one connection.</param>
/// <param name="picker">Asks the user for the file.</param>
/// <param name="localisation">Words a save failure.</param>
internal sealed class ExportCommand(Func<IIpcClient> createClient, IFilePicker picker, ILocalisationService localisation)
{
    /// <summary>Asks for a file first, so a dismissed dialog costs the daemon nothing, then writes the daemon's answer to it, replacing the file when it exists.</summary>
    /// <param name="query">The export to ask for.</param>
    /// <param name="suggestedName">The file name the dialog offers.</param>
    /// <param name="cancellationToken">Cancelling abandons the dialog, the wait or the write and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Success when the file was written or the user dismissed the dialog; otherwise the daemon failure, or a save failure worded for the user.</returns>
    public async Task<Result<Unit, ConnectFailure>> RunAsync(IQuery<ExportView> query, string suggestedName, CancellationToken cancellationToken)
        => await Option.FromNullable(await picker.PickSaveFileAsync(suggestedName, cancellationToken)).Match(
            path => ExportAsync(query, path, cancellationToken),
            () => Task.FromResult<Result<Unit, ConnectFailure>>(new Ok<Unit>(default)));

    private async Task<Result<Unit, ConnectFailure>> ExportAsync(IQuery<ExportView> query, string path, CancellationToken cancellationToken)
    {
        await using var client = createClient();

        return await FetchAsync(client, query, cancellationToken) switch
        {
            Ok<ExportView> ok => await SaveAsync(path, ok.Value.Csv, cancellationToken),
            Fail<ConnectFailure> fail => fail
        };
    }

    private static async Task<Result<ExportView, ConnectFailure>> FetchAsync(IIpcClient client, IQuery<ExportView> query, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await client.QueryAsync(query, cancellationToken) switch
            {
                Ok<ExportView> ok => ok,
                Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
            },
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<Unit, ConnectFailure>> SaveAsync(string path, string csv, CancellationToken cancellationToken)
        => (await Try.RunAsync(async token =>
        {
            await File.WriteAllTextAsync(path, csv, token);

            return default(Unit);
        }, cancellationToken)).ToResult(exception => new ConnectFailure(localisation.Format("Operations.ExportFailed", ("path", path), ("reason", exception.Message))));
}

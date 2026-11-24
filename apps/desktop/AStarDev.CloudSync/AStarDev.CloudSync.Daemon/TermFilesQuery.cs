using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetTermFiles"/> from the <see cref="IFileClassificationStore"/>.</summary>
internal sealed class TermFilesQuery(IFileClassificationStore links)
{
    private const int MaxTake = 500;

    /// <summary>Reads one page of the files linked to the term.</summary>
    /// <returns>The page; a negative <see cref="GetTermFiles.Skip"/> counts as zero and a <see cref="GetTermFiles.Take"/> outside 1 to 500 is brought into that range. <see cref="HandlerFailed"/> carries the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<TermFilesView, IpcError>> HandleAsync(GetTermFiles query, CancellationToken cancellationToken)
        => await links.GetFilesForTermAsync(new TermId(query.TermId), Math.Max(query.Skip, 0), Math.Clamp(query.Take, 1, MaxTake), cancellationToken) switch
        {
            Ok<TermFilesPage> page => new Ok<TermFilesView>(new TermFilesView(page.Value.Total, [.. page.Value.Files.Select(file => new TermFileView(file.ItemId.Value, file.Path.Value, file.AccountName))])),
            Fail<ClassificationError> fail => new Fail<IpcError>(new HandlerFailed(ClassificationErrorMessages.Describe(fail.Error, "read", "files of the term")))
        };
}

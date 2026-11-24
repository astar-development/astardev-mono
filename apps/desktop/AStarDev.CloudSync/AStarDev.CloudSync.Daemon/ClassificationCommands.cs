using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Answers the classification commands the user gives: assigning a term to a file, promoting or ignoring a suggested word, and adding, renaming and removing categories and terms.
/// Every method answers <see cref="CommandOutcome.Accepted"/> once done, <see cref="CommandOutcome.ValidationError"/> when the text given is blank, or <see cref="CommandOutcome.Rejected"/> with the failure worded for the user.
/// Cancellation propagates as <see cref="OperationCanceledException"/>.
/// </summary>
/// <param name="categories">Stores categories.</param>
/// <param name="terms">Stores terms.</param>
/// <param name="links">Stores the links between files and terms.</param>
/// <param name="suggestions">Promotes and ignores words.</param>
/// <param name="editor">Edits terms.</param>
/// <param name="logger">Reports a refused command.</param>
internal sealed class ClassificationCommands(ICategoryStore categories, ITermStore terms, IFileClassificationStore links, IClassificationSuggestions suggestions, IClassificationTermEditor editor, ILogger<ClassificationCommands> logger)
{
    private const string BlankCategoryName = "A category needs a name.";

    /// <summary>Links the term to the file as the user's own choice.</summary>
    public async Task<Result<CommandResult, IpcError>> AssignAsync(AssignTerm command, CancellationToken cancellationToken)
        => Answer(await links.AssignUserLinkAsync(new ItemId(command.ItemId), new TermId(command.TermId), cancellationToken), "saved", "term of the file");

    /// <summary>Makes the word an active user term.</summary>
    public async Task<Result<CommandResult, IpcError>> PromoteAsync(PromoteSuggestion command, CancellationToken cancellationToken)
        => Answer(await suggestions.PromoteAsync(command.Word, new CategoryId(command.CategoryId), cancellationToken), "saved", "term");

    /// <summary>Marks the word ignored.</summary>
    public async Task<Result<CommandResult, IpcError>> IgnoreAsync(IgnoreSuggestion command, CancellationToken cancellationToken)
        => Answer(await suggestions.IgnoreAsync(command.Word, new CategoryId(command.CategoryId), cancellationToken), "saved", "term");

    /// <summary>Adds a category.</summary>
    public async Task<Result<CommandResult, IpcError>> CreateCategoryAsync(CreateCategory command, CancellationToken cancellationToken)
        => command.Name.Trim() is { Length: > 0 } name
            ? Answer(await categories.AddAsync(new Category(new CategoryId(Guid.NewGuid()), name), cancellationToken), "saved", "category")
            : Validation(BlankCategoryName);

    /// <summary>Renames a category.</summary>
    public async Task<Result<CommandResult, IpcError>> RenameCategoryAsync(RenameCategory command, CancellationToken cancellationToken)
        => command.Name.Trim() is { Length: > 0 } name
            ? Answer(await categories.RenameAsync(new CategoryId(command.CategoryId), name, cancellationToken), "saved", "category")
            : Validation(BlankCategoryName);

    /// <summary>Removes a category that holds no terms.</summary>
    public async Task<Result<CommandResult, IpcError>> DeleteCategoryAsync(DeleteCategory command, CancellationToken cancellationToken)
        => Answer(await categories.RemoveAsync(new CategoryId(command.CategoryId), cancellationToken), "removed", "category");

    /// <summary>Adds an active user term.</summary>
    public async Task<Result<CommandResult, IpcError>> CreateTermAsync(CreateTerm command, CancellationToken cancellationToken)
        => Answer(await suggestions.PromoteAsync(command.Text, new CategoryId(command.CategoryId), cancellationToken), "saved", "term");

    /// <summary>Edits a term.</summary>
    public async Task<Result<CommandResult, IpcError>> UpdateTermAsync(UpdateTerm command, CancellationToken cancellationToken)
        => Answer(await editor.UpdateAsync(new TermId(command.TermId), command.Text, new CategoryId(command.CategoryId), command.Ignored, cancellationToken), "saved", "term");

    /// <summary>Removes a term and its links.</summary>
    public async Task<Result<CommandResult, IpcError>> DeleteTermAsync(DeleteTerm command, CancellationToken cancellationToken)
        => Answer(await terms.RemoveAsync(new TermId(command.TermId), cancellationToken), "removed", "term");

    private Result<CommandResult, IpcError> Answer<T>(Result<T, ClassificationError> result, string failedAction, string subject) => result switch
    {
        Ok<T> => new Ok<CommandResult>(new CommandResult(CommandOutcome.Accepted)),
        Fail<ClassificationError> fail => Refuse(fail.Error, failedAction, subject)
    };

    private Ok<CommandResult> Refuse(ClassificationError error, string failedAction, string subject)
    {
        var message = ClassificationErrorMessages.Describe(error, failedAction, subject);
        DaemonLog.ClassificationCommandRefused(logger, message, ErrorInfo.CodeOf(error.Value));

        return new Ok<CommandResult>(new CommandResult(error.Value is InvalidTermText ? CommandOutcome.ValidationError : CommandOutcome.Rejected, message));
    }

    private static Ok<CommandResult> Validation(string message) => new(new CommandResult(CommandOutcome.ValidationError, message));
}

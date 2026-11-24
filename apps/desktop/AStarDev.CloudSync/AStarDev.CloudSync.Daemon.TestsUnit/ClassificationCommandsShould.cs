using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Daemon.TestsUnit.ClassificationDaemonTestData;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ClassificationCommandsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task link_a_term_to_a_file_as_the_users_choice()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Photos/a.txt");
        var jpg = await TermAsync(host, "jpg");
        var file = (await host.Services.GetRequiredService<IClassifiableFileSource>().GetPageAsync(null, 10, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<ClassifiableFile>>>().Value.Single();

        var outcome = await Commands(host).AssignAsync(new AssignTerm(file.ItemId.Value, jpg.Id.Value), Token);

        Accepted(outcome);
        (await host.Services.GetRequiredService<IFileClassificationStore>().GetTermsForItemAsync(file.ItemId, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<TermId>>>().Value.ShouldContain(jpg.Id);
    }

    [Fact]
    public async Task refuse_a_term_for_a_file_that_is_not_stored()
    {
        await using var host = await SeededHostAsync(Token);
        var jpg = await TermAsync(host, "jpg");

        var outcome = await Commands(host).AssignAsync(new AssignTerm(Guid.NewGuid(), jpg.Id.Value), Token);

        Outcome(outcome).ShouldBe(CommandOutcome.Rejected);
    }

    [Fact]
    public async Task promote_and_ignore_a_suggested_word()
    {
        await using var host = await SeededHostAsync(Token);
        var category = await FirstCategoryAsync(host);

        Accepted(await Commands(host).PromoteAsync(new PromoteSuggestion("zorblax", category.Id.Value), Token));
        Accepted(await Commands(host).IgnoreAsync(new IgnoreSuggestion("plimbus", category.Id.Value), Token));

        (await TermAsync(host, "zorblax")).Status.ShouldBe(TermStatus.Active);
        (await TermAsync(host, "plimbus")).Status.ShouldBe(TermStatus.Ignored);
    }

    [Fact]
    public async Task add_rename_and_remove_a_category()
    {
        await using var host = await SeededHostAsync(Token);
        var commands = Commands(host);

        Accepted(await commands.CreateCategoryAsync(new CreateCategory("  Weather "), Token));
        var created = (await Categories(host)).Single(category => category.Name == "Weather");
        Accepted(await commands.RenameCategoryAsync(new RenameCategory(created.Id.Value, "Climate"), Token));
        Accepted(await commands.DeleteCategoryAsync(new DeleteCategory(created.Id.Value), Token));

        var names = (await Categories(host)).Select(category => category.Name).ToList();
        names.ShouldNotContain("Weather");
        names.ShouldNotContain("Climate");
    }

    [Fact]
    public async Task refuse_a_blank_or_taken_category_name_and_a_category_that_holds_terms()
    {
        await using var host = await SeededHostAsync(Token);
        var commands = Commands(host);
        var first = await FirstCategoryAsync(host);

        Outcome(await commands.CreateCategoryAsync(new CreateCategory("   "), Token)).ShouldBe(CommandOutcome.ValidationError);
        Outcome(await commands.CreateCategoryAsync(new CreateCategory(first.Name.ToUpperInvariant()), Token)).ShouldBe(CommandOutcome.Rejected);
        Outcome(await commands.DeleteCategoryAsync(new DeleteCategory(first.Id.Value), Token)).ShouldBe(CommandOutcome.Rejected);
    }

    [Fact]
    public async Task add_edit_and_remove_a_term()
    {
        await using var host = await SeededHostAsync(Token);
        var commands = Commands(host);
        var category = await FirstCategoryAsync(host);

        Accepted(await commands.CreateTermAsync(new CreateTerm("  Zorblax  Quillon ", category.Id.Value), Token));
        var created = await TermAsync(host, "zorblax quillon");
        Accepted(await commands.UpdateTermAsync(new UpdateTerm(created.Id.Value, "Plimbus", category.Id.Value, true), Token));
        var edited = await TermAsync(host, "plimbus");
        Accepted(await commands.DeleteTermAsync(new DeleteTerm(edited.Id.Value), Token));

        (edited.Id, edited.Status, edited.Source).ShouldBe((created.Id, TermStatus.Ignored, TermSource.User));
        (await Terms(host)).Select(term => term.Id).ShouldNotContain(created.Id);
    }

    [Fact]
    public async Task refuse_blank_term_text_and_a_term_that_is_not_stored()
    {
        await using var host = await SeededHostAsync(Token);
        var commands = Commands(host);
        var category = await FirstCategoryAsync(host);

        Outcome(await commands.CreateTermAsync(new CreateTerm("  ", category.Id.Value), Token)).ShouldBe(CommandOutcome.ValidationError);
        Outcome(await commands.UpdateTermAsync(new UpdateTerm(Guid.NewGuid(), "red", category.Id.Value, false), Token)).ShouldBe(CommandOutcome.Rejected);
        Outcome(await commands.DeleteTermAsync(new DeleteTerm(Guid.NewGuid()), Token)).ShouldBe(CommandOutcome.Rejected);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await SeededHostAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => Commands(host).CreateCategoryAsync(new CreateCategory("Weather"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => Commands(host).DeleteTermAsync(new DeleteTerm(Guid.NewGuid()), cancelled.Token));
    }

    private static ClassificationCommands Commands(DaemonTestHost host) => host.Services.GetRequiredService<ClassificationCommands>();

    private static void Accepted(Result<CommandResult, IpcError> result) => Outcome(result).ShouldBe(CommandOutcome.Accepted);

    private static CommandOutcome Outcome(Result<CommandResult, IpcError> result) => result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome;

    private static async Task<IReadOnlyList<Term>> Terms(DaemonTestHost host) => (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value;

    private static async Task<Term> TermAsync(DaemonTestHost host, string text) => (await Terms(host)).Single(term => term.Text == text);

    private static async Task<IReadOnlyList<Category>> Categories(DaemonTestHost host) => (await host.Services.GetRequiredService<ICategoryStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Category>>>().Value;

    private static async Task<Category> FirstCategoryAsync(DaemonTestHost host) => (await Categories(host)).First();
}

using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Onboarding;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class OnboardingViewModelShould
{
    private static readonly AccountView Account = new(Guid.NewGuid(), "ada@example.com", "OneDrive", AccountState.Connected);
    private static readonly SyncJobView Job = new(Guid.NewGuid(), Account.Id, "OneDrive", "/home/ada/OneDrive", SyncBootstrapMode.Merge);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record Rig(OnboardingViewModel Onboarding, FakeIpcClient Client, FakeNavigationService Navigation, ConnectViewModel Connect, ScopeSelectionViewModel Selection, AccountsViewModel Accounts);

    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static Rig CreateRig(FakeIpcClient? client = null, FakeIpcClient? connectClient = null)
    {
        client ??= new FakeIpcClient([], available: true) { AccountsReply = new Ok<AccountsView>(new AccountsView([Account])), JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])) };
        var navigation = new FakeNavigationService();
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        var connect = new ConnectViewModel(() => connectClient ?? client);
        var accounts = new AccountsViewModel(() => client);
        var folders = new FolderTreeViewModel(() => client, selection);
        var connection = new ConnectionViewModel(() => client, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
        var onboarding = new OnboardingViewModel(RealLocalisation(), () => navigation, connection, accounts, connect, folders, selection);

        return new Rig(onboarding, client, navigation, connect, selection, accounts);
    }

    private static async Task ReachReviewAsync(Rig rig)
    {
        rig.Onboarding.Next();
        await rig.Connect.ConnectAsync(Token);
        await rig.Accounts.RefreshAsync(Token);
        rig.Onboarding.Next();
        rig.Selection.LocalRoot = Job.LocalRootPath;
        rig.Selection.Select("docs", true);
        rig.Onboarding.Next();
    }

    [Fact]
    public async Task follow_the_documented_step_order()
    {
        var rig = CreateRig();
        var seen = new List<OnboardingStep> { rig.Onboarding.Step };

        rig.Onboarding.Next();
        seen.Add(rig.Onboarding.Step);
        await rig.Connect.ConnectAsync(Token);
        rig.Onboarding.Next();
        seen.Add(rig.Onboarding.Step);
        rig.Selection.LocalRoot = Job.LocalRootPath;
        rig.Selection.Select("docs", true);
        rig.Onboarding.Next();
        seen.Add(rig.Onboarding.Step);
        await rig.Onboarding.ConfirmAsync(Token);
        seen.Add(rig.Onboarding.Step);
        rig.Onboarding.Finish();

        seen.ShouldBe([OnboardingStep.Welcome, OnboardingStep.ConnectProvider, OnboardingStep.SelectScopes, OnboardingStep.Review, OnboardingStep.InitialSync]);
        rig.Navigation.Visited.ShouldBe([ScreenId.Dashboard]);
        rig.Onboarding.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task not_start_synchronisation_before_review_is_confirmed()
    {
        var rig = CreateRig();

        await ReachReviewAsync(rig);

        rig.Onboarding.Step.ShouldBe(OnboardingStep.Review);
        rig.Client.Sent.ShouldNotContain(command => command is CreateSyncJob || command is AddSyncScope);
        rig.Selection.HasJob.ShouldBeFalse();

        await rig.Onboarding.ConfirmAsync(Token);

        rig.Client.Sent.ShouldContain(command => command is CreateSyncJob);
        rig.Onboarding.Step.ShouldBe(OnboardingStep.InitialSync);
    }

    [Fact]
    public async Task not_confirm_anything_before_the_review_step()
    {
        var rig = CreateRig();

        await rig.Onboarding.ConfirmAsync(Token);

        rig.Client.Sent.ShouldBeEmpty();
        rig.Onboarding.Step.ShouldBe(OnboardingStep.Welcome);
    }

    [Fact]
    public void not_leave_the_connect_step_before_an_account_is_connected()
    {
        var rig = CreateRig();
        rig.Onboarding.Next();

        rig.Onboarding.CanContinue.ShouldBeFalse();
        rig.Onboarding.Next();

        rig.Onboarding.Step.ShouldBe(OnboardingStep.ConnectProvider);
    }

    [Fact]
    public async Task not_leave_the_scope_step_without_a_scope_and_a_local_folder()
    {
        var rig = CreateRig();
        rig.Onboarding.Next();
        await rig.Connect.ConnectAsync(Token);
        rig.Onboarding.Next();

        rig.Onboarding.CanContinue.ShouldBeFalse();
        rig.Selection.Select("docs", true);
        rig.Onboarding.CanContinue.ShouldBeFalse();
        rig.Selection.LocalRoot = Job.LocalRootPath;
        rig.Onboarding.CanContinue.ShouldBeTrue();
    }

    [Fact]
    public void go_back_one_step_but_not_before_the_welcome_step()
    {
        var rig = CreateRig();

        rig.Onboarding.CanGoBack.ShouldBeFalse();
        rig.Onboarding.Next();
        rig.Onboarding.CanGoBack.ShouldBeTrue();
        rig.Onboarding.Back();

        rig.Onboarding.Step.ShouldBe(OnboardingStep.Welcome);
    }

    [Fact]
    public async Task summarise_provider_local_path_scopes_and_labelled_placeholders_on_review()
    {
        var rig = CreateRig();

        await ReachReviewAsync(rig);

        rig.Onboarding.ReviewProvider.ShouldContain("ada@example.com");
        rig.Onboarding.ReviewLocalPath.ShouldBe(Job.LocalRootPath);
        rig.Onboarding.ReviewScopes.ShouldBe(["docs"]);
        rig.Onboarding.EstimatedItemsText.ShouldContain("placeholder");
        rig.Onboarding.AvailableStorageText.ShouldContain("placeholder");
    }

    [Fact]
    public async Task stay_on_review_and_say_why_when_the_daemon_rejects_the_job()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = new Ok<AccountsView>(new AccountsView([Account])), CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.ValidationError, "The local folder does not exist.")) };
        var rig = CreateRig(client, new FakeIpcClient([], available: true));
        await ReachReviewAsync(rig);

        await rig.Onboarding.ConfirmAsync(Token);

        rig.Onboarding.Step.ShouldBe(OnboardingStep.Review);
        rig.Selection.StatusMessage.ShouldBe("The local folder does not exist.");
    }

    [Fact]
    public async Task show_itself_on_first_run_when_no_account_exists()
    {
        var rig = CreateRig(new FakeIpcClient([], available: true));

        await rig.Onboarding.EvaluateFirstRunAsync(Token);

        rig.Onboarding.IsActive.ShouldBeTrue();
        rig.Onboarding.Step.ShouldBe(OnboardingStep.Welcome);
    }

    [Fact]
    public async Task stay_hidden_when_an_account_exists()
    {
        var rig = CreateRig();

        await rig.Onboarding.EvaluateFirstRunAsync(Token);

        rig.Onboarding.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task stay_hidden_when_the_daemon_cannot_be_asked()
    {
        var rig = CreateRig(new FakeIpcClient([], available: false));

        await rig.Onboarding.EvaluateFirstRunAsync(Token);

        rig.Onboarding.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task close_without_sending_anything_when_skipped()
    {
        var rig = CreateRig(new FakeIpcClient([], available: true));
        await rig.Onboarding.EvaluateFirstRunAsync(Token);

        rig.Onboarding.Skip();

        rig.Onboarding.IsActive.ShouldBeFalse();
        rig.Client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public void be_composed_into_the_main_view_model_and_hidden_until_the_first_run_check()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        using var host = builder.Build();

        var main = host.Services.GetRequiredService<MainViewModel>();

        main.Onboarding.ShouldBeSameAs(host.Services.GetRequiredService<OnboardingViewModel>());
        main.Onboarding.IsActive.ShouldBeFalse();
        main.Onboarding.ProgressText.ShouldNotStartWith("!");
        main.Onboarding.EstimatedItemsText.ShouldNotStartWith("!");
        main.Onboarding.PhaseText.ShouldNotStartWith("!");
    }

    [Fact]
    public async Task honour_cancellation_when_evaluating_first_run()
    {
        var rig = CreateRig();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => rig.Onboarding.EvaluateFirstRunAsync(cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_confirming()
    {
        var rig = CreateRig();
        await ReachReviewAsync(rig);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => rig.Onboarding.ConfirmAsync(cancelled.Token));

        rig.Onboarding.Step.ShouldBe(OnboardingStep.Review);
    }
}

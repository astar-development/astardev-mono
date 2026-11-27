using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ScopeSelectionViewModelShould
{
    private static readonly AccountView Account = new(Guid.NewGuid(), "ada@example.com", "OneDrive", AccountState.Connected);
    private static readonly SyncJobView Job = new(Guid.NewGuid(), Account.Id, "OneDrive", "/home/ada/OneDrive", SyncBootstrapMode.Upload);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void start_with_nothing_selected()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: true), new FakeFolderPicker(null));

        viewModel.SelectedFolderIds.ShouldBeEmpty();
        viewModel.HasJob.ShouldBeFalse();
        viewModel.LocalRoot.ShouldBeEmpty();
        viewModel.BootstrapMode.ShouldBe(SyncBootstrapMode.Merge);
        viewModel.CanCreateJob.ShouldBeFalse();
    }

    [Fact]
    public async Task put_the_picked_folder_in_the_local_root()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: true), new FakeFolderPicker("/home/ada/OneDrive"));

        await viewModel.BrowseAsync(Token);

        viewModel.LocalRoot.ShouldBe("/home/ada/OneDrive");
    }

    [Fact]
    public async Task keep_the_typed_local_root_when_the_picker_is_cancelled()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: true), new FakeFolderPicker(null)) { LocalRoot = "/typed" };

        await viewModel.BrowseAsync(Token);

        viewModel.LocalRoot.ShouldBe("/typed");
    }

    [Fact]
    public void offer_to_create_a_job_once_a_folder_is_ticked_and_a_local_root_is_given()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: true), new FakeFolderPicker(null));

        viewModel.LocalRoot = "/home/ada/OneDrive";
        viewModel.CanCreateJob.ShouldBeFalse();
        viewModel.Select("docs", true);

        viewModel.CanCreateJob.ShouldBeTrue();
        viewModel.SelectedFolderIds.ShouldBe(["docs"]);
    }

    [Fact]
    public async Task show_only_the_job_of_the_chosen_account()
    {
        var other = new SyncJobView(Guid.NewGuid(), Guid.NewGuid(), "Other", "/home/bob/OneDrive", SyncBootstrapMode.Download);
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([other, Job])) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { AccountId = Account.Id };

        await viewModel.RefreshAsync(Token);

        viewModel.LocalRoot.ShouldBe(Job.LocalRootPath);
    }

    [Fact]
    public async Task show_no_job_for_an_account_that_has_none()
    {
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { AccountId = Guid.NewGuid() };

        await viewModel.RefreshAsync(Token);

        viewModel.HasJob.ShouldBeFalse();
        viewModel.LocalRoot.ShouldBeEmpty();
    }

    [Fact]
    public void forget_the_choices_of_the_previous_account_when_the_account_changes()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: true), new FakeFolderPicker(null)) { AccountId = Account.Id, LocalRoot = "/home/ada/OneDrive" };
        viewModel.Select("docs", true);

        viewModel.AccountId = Guid.NewGuid();

        viewModel.SelectedFolderIds.ShouldBeEmpty();
        viewModel.LocalRoot.ShouldBeEmpty();
    }

    [Fact]
    public async Task create_the_job_for_the_chosen_account_not_the_first()
    {
        var first = new AccountView(Guid.NewGuid(), "first@example.com", "OneDrive", AccountState.Connected);
        var client = new FakeIpcClient([], available: true) { AccountsReply = new Ok<AccountsView>(new AccountsView([first, Account])), JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { AccountId = Account.Id, LocalRoot = Job.LocalRootPath };
        viewModel.Select("docs", true);

        await viewModel.CreateJobAsync(Token);

        client.Sent.OfType<CreateSyncJob>().ShouldHaveSingleItem().AccountId.ShouldBe(Account.Id);
    }

    [Fact]
    public async Task create_the_job_with_the_mode_and_add_each_ticked_folder()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = new Ok<AccountsView>(new AccountsView([Account])), JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { LocalRoot = Job.LocalRootPath, BootstrapMode = SyncBootstrapMode.Upload };
        viewModel.Select("docs", true);
        viewModel.Select("notes", true);

        await viewModel.CreateJobAsync(Token);

        client.Sent.ShouldBe([new CreateSyncJob(Account.Id, Job.LocalRootPath, SyncBootstrapMode.Upload), new AddSyncScope(Job.Id, "docs"), new AddSyncScope(Job.Id, "notes")]);
        viewModel.HasJob.ShouldBeTrue();
        viewModel.CanCreateJob.ShouldBeFalse();
        viewModel.StatusMessage.ShouldBeNull();
    }

    [Fact]
    public async Task show_the_reason_when_the_daemon_rejects_the_job()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = new Ok<AccountsView>(new AccountsView([Account])), CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.ValidationError, "The local folder does not exist.")) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { LocalRoot = "/missing" };
        viewModel.Select("docs", true);

        await viewModel.CreateJobAsync(Token);

        viewModel.StatusMessage.ShouldBe("The local folder does not exist.");
        viewModel.HasJob.ShouldBeFalse();
        client.Sent.ShouldHaveSingleItem().ShouldBeOfType<CreateSyncJob>();
    }

    [Fact]
    public async Task ask_for_an_account_when_none_is_connected()
    {
        var client = new FakeIpcClient([], available: true);
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { LocalRoot = "/home/ada/OneDrive" };
        viewModel.Select("docs", true);

        await viewModel.CreateJobAsync(Token);

        viewModel.StatusMessage.ShouldBe("Connect an account first.");
        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task show_the_stored_job_and_selection_after_a_refresh()
    {
        var client = new FakeIpcClient([], available: true)
        {
            JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])),
            ScopesReply = _ => new Ok<SyncScopesView>(new SyncScopesView([new SyncScopeView("docs"), new SyncScopeView("notes")], ["Photos"]))
        };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));

        await viewModel.RefreshAsync(Token);

        viewModel.HasJob.ShouldBeTrue();
        viewModel.LocalRoot.ShouldBe(Job.LocalRootPath);
        viewModel.BootstrapMode.ShouldBe(SyncBootstrapMode.Upload);
        viewModel.SelectedFolderIds.ShouldBe(["docs", "notes"]);
        viewModel.IsSelected("docs").ShouldBeTrue();
        viewModel.IsSelected("other").ShouldBeFalse();
        viewModel.UnsyncedFolders.ShouldBe(["Photos"]);
        viewModel.HasUnsyncedFolders.ShouldBeTrue();
    }

    [Fact]
    public async Task add_or_remove_the_scope_at_once_when_a_job_exists()
    {
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        await viewModel.RefreshAsync(Token);

        await viewModel.ApplyAsync(new ScopeChange("docs", true), Token);
        await viewModel.ApplyAsync(new ScopeChange("docs", false), Token);

        client.Sent.ShouldBe([new AddSyncScope(Job.Id, "docs"), new RemoveSyncScope(Job.Id, "docs")]);
    }

    [Fact]
    public async Task send_every_tick_when_ticks_follow_each_other_quickly()
    {
        var hold = new TaskCompletionSource();
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])), HoldCommand = hold };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        await viewModel.RefreshAsync(Token);
        viewModel.Select("docs", true);
        var first = viewModel.ApplyCommand.ExecutionTask!;
        viewModel.Select("notes", true);
        var second = viewModel.ApplyCommand.ExecutionTask!;

        hold.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), Token);

        first.IsCompletedSuccessfully.ShouldBeTrue();
        second.IsCompletedSuccessfully.ShouldBeTrue();
        client.Sent.ShouldBe([new AddSyncScope(Job.Id, "docs"), new AddSyncScope(Job.Id, "notes")]);
    }

    [Fact]
    public async Task show_the_reason_and_reload_when_the_daemon_rejects_a_scope_change()
    {
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])), CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "That folder is already selected.")) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        await viewModel.RefreshAsync(Token);
        viewModel.Select("docs", true);

        await viewModel.ApplyAsync(new ScopeChange("docs", true), Token);

        viewModel.StatusMessage.ShouldBe("That folder is already selected.");
        viewModel.SelectedFolderIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task show_an_error_when_the_daemon_is_not_running()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: false), new FakeFolderPicker(null));

        await viewModel.RefreshAsync(Token);

        viewModel.StatusMessage!.ShouldContain("daemon");
        viewModel.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var viewModel = new ScopeSelectionViewModel(() => new FakeIpcClient([], available: true), new FakeFolderPicker("/x")) { LocalRoot = "/x" };
        viewModel.Select("docs", true);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.RefreshAsync(cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.CreateJobAsync(cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.BrowseAsync(cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_applying_a_scope_change()
    {
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([Job])) };
        var viewModel = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null));
        await viewModel.RefreshAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.ApplyAsync(new ScopeChange("docs", true), cancelled.Token));
    }
}

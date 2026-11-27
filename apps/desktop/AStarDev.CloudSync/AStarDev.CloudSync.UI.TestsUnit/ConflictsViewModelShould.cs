using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ConflictsViewModelShould
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Job = Guid.NewGuid();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static ConflictView View(string type = "ModifyModify", string status = "Open", string path = "Reports/Q1.xlsx", TimeSpan? age = null, ConflictSideView? local = null, ConflictSideView? remote = null, bool bothSides = true)
        => new(Guid.NewGuid(), Job, Guid.NewGuid(), type, Now - (age ?? TimeSpan.FromHours(5)), status, path, bothSides ? local ?? new ConflictSideView(path, Now.AddHours(-30), 48_128) : local, bothSides ? remote ?? new ConflictSideView(path, Now.AddHours(-20), 51_200) : remote);

    private static (IHost Host, ConflictsViewModel Screen, ConnectionViewModel Connection, FakeIpcClient Client) Create(params ConflictView[] conflicts)
    {
        var client = new FakeIpcClient([], available: true) { JobsReply = new Ok<SyncJobsView>(new SyncJobsView([new SyncJobView(Job, Guid.NewGuid(), "OneDrive", "/home/ada/OneDrive", SyncBootstrapMode.Merge)])) };
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(conflicts.Length, conflicts));
        var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));
        var connection = host.Services.GetRequiredService<ConnectionViewModel>();
        connection.ReplaceConflicts(ConflictPages.Of(conflicts));

        return (host, host.Screen<ConflictsViewModel>(), connection, client);
    }

    [Fact]
    public void list_the_open_and_deferred_conflicts_oldest_first()
    {
        var fresh = View(path: "b.txt", age: TimeSpan.FromHours(2));
        var deferred = View(status: "Deferred", path: "a.txt", age: TimeSpan.FromDays(3));
        var (host, screen, _, _) = Create(fresh, deferred);
        using var lifetime = host;

        screen.Rows.Select(row => row.Name).ShouldBe(["a.txt", "b.txt"]);
        screen.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void say_there_is_nothing_to_decide_when_there_are_no_conflicts()
    {
        var (host, screen, _, _) = Create();
        using var lifetime = host;

        screen.IsEmpty.ShouldBeTrue();
        screen.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void show_the_name_path_type_times_and_sizes_of_both_sides()
    {
        var local = new ConflictSideView("Reports/Q1.xlsx", new DateTimeOffset(2026, 11, 22, 8, 14, 0, TimeSpan.Zero), 2_048);
        var remote = new ConflictSideView("Archive/Q1.xlsx", new DateTimeOffset(2026, 11, 22, 8, 17, 0, TimeSpan.Zero), 3_000);
        var (host, screen, _, _) = Create(View(type: "RenameRename", path: "Archive/Q1.xlsx", age: TimeSpan.FromDays(2), local: local, remote: remote));
        using var lifetime = host;

        var row = screen.Rows.Single();

        row.Name.ShouldBe("Q1.xlsx");
        row.PathText.ShouldBe("Archive/Q1.xlsx");
        row.TypeText.ShouldBe("Rename conflict");
        row.LocalPath.ShouldBe("Reports/Q1.xlsx");
        row.RemotePath.ShouldBe("Archive/Q1.xlsx");
        row.LocalModifiedText.ShouldBe("22/11/2026 08:14");
        row.RemoteModifiedText.ShouldBe("22/11/2026 08:17");
        row.LocalSizeText.ShouldBe("2 KiB");
        row.RemoteSizeText.ShouldBe("3 KiB");
        row.DetectedText.ShouldBe("Detected 20/11/2026 12:00");
        row.AgeText.ShouldBe("Open for 2 d");
    }

    [Theory]
    [InlineData("ModifyModify", true, true, "Concurrent modification", "changed locally and in OneDrive")]
    [InlineData("RenameRename", true, true, "Rename conflict", "moved to different locations")]
    [InlineData("KeepDelete", false, true, "Delete conflict", "removed locally while OneDrive changed")]
    [InlineData("KeepDelete", true, false, "Delete conflict", "removed in OneDrive while it changed locally")]
    [InlineData("CreateCreate", true, true, "Created on both sides", "created on this computer and in OneDrive")]
    [InlineData("SomethingNew", true, true, "Conflict", "cannot safely decide")]
    public void explain_what_happened_and_why_for_each_category(string type, bool hasLocal, bool hasRemote, string expectedType, string expectedReasonPart)
    {
        var side = new ConflictSideView("a.txt", Now, 10);
        var (host, screen, _, _) = Create(View(type: type, path: "a.txt", local: hasLocal ? side : null, remote: hasRemote ? side : null, bothSides: false));
        using var lifetime = host;

        var row = screen.Rows.Single();

        row.TypeText.ShouldBe(expectedType);
        row.ReasonText.ShouldContain(expectedReasonPart);
        row.ReasonText.ShouldNotStartWith("!");
    }

    [Fact]
    public void show_a_missing_side_as_not_present()
    {
        var (host, screen, _, _) = Create(View(type: "KeepDelete", remote: null, bothSides: false, local: new ConflictSideView("a.txt", Now, 10)));
        using var lifetime = host;

        var row = screen.Rows.Single();

        row.RemotePath.ShouldBe("Not present");
        row.RemoteModifiedText.ShouldBe("Not present");
        row.RemoteSizeText.ShouldBe("Not present");
    }

    [Fact]
    public void tell_a_deferred_conflict_from_a_fresh_one_without_relying_on_colour()
    {
        var (host, screen, _, _) = Create(View(status: "Open", age: TimeSpan.FromDays(2)), View(status: "Deferred", age: TimeSpan.FromDays(1)));
        using var lifetime = host;

        var open = screen.Rows[0];
        var deferred = screen.Rows[1];

        open.IsDeferred.ShouldBeFalse();
        deferred.IsDeferred.ShouldBeTrue();
        open.Status.Text.ShouldBe("Unresolved");
        deferred.Status.Text.ShouldBe("Deferred");
        open.Status.IconKey.ShouldNotBe(deferred.Status.IconKey);
    }

    [Fact]
    public void offer_the_four_actions_and_promote_keep_both()
    {
        var (host, screen, _, _) = Create(View());
        using var lifetime = host;

        screen.Actions.Select(action => action.Choice).ShouldBe([ConflictChoice.KeepLocal, ConflictChoice.KeepRemote, ConflictChoice.KeepBoth, ConflictChoice.Defer]);
        screen.Actions.Select(action => action.Label).ShouldBe(["Keep local", "Keep remote", "Keep both (recommended)", "Defer"]);
        screen.RecoveryText.ShouldNotStartWith("!");
    }

    [Fact]
    public void enable_the_actions_only_when_a_conflict_is_focused_or_ticked()
    {
        var (host, screen, _, _) = Create(View(), View());
        using var lifetime = host;

        screen.Actions.ShouldAllBe(action => !action.IsEnabled);

        screen.SelectedConflict = screen.Rows[0];
        screen.Actions.ShouldAllBe(action => action.IsEnabled);

        screen.SelectedConflict = null;
        screen.Rows[1].IsSelected = true;
        screen.Actions.ShouldAllBe(action => action.IsEnabled);

        screen.ClearSelection();
        screen.Actions.ShouldAllBe(action => !action.IsEnabled);
    }

    [Fact]
    public void ask_for_confirmation_and_send_nothing_until_confirmed()
    {
        var (host, screen, _, client) = Create(View());
        using var lifetime = host;
        screen.SelectedConflict = screen.Rows[0];

        screen.RequestResolution(ConflictChoice.KeepBoth);

        screen.IsConfirming.ShouldBeTrue();
        screen.PendingChoice.ShouldBe(ConflictChoice.KeepBoth);
        screen.ConfirmationText.ShouldBe("Apply \"Keep both (recommended)\" to 1 conflict(s)? The other version stays recoverable.");
        client.Sent.ShouldBeEmpty();

        screen.CancelResolution();

        screen.IsConfirming.ShouldBeFalse();
        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public void ignore_a_resolution_request_when_there_is_no_target()
    {
        var (host, screen, _, _) = Create(View());
        using var lifetime = host;

        screen.RequestResolution(ConflictChoice.KeepLocal);

        screen.IsConfirming.ShouldBeFalse();
    }

    [Fact]
    public async Task resolve_the_focused_conflict_and_list_what_remains()
    {
        var first = View(path: "a.txt", age: TimeSpan.FromDays(2));
        var second = View(path: "b.txt", age: TimeSpan.FromDays(1));
        var (host, screen, connection, client) = Create(first, second);
        using var lifetime = host;
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(1, [second]));
        screen.SelectedConflict = screen.Rows[0];

        screen.RequestResolution(ConflictChoice.KeepLocal);
        await screen.ConfirmAsync(Token);

        AssertSent(client, [first.ConflictId], ConflictChoice.KeepLocal);
        connection.Conflicts.ShouldBe([second]);
        screen.Rows.Select(row => row.Name).ShouldBe(["b.txt"]);
        screen.IsConfirming.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
        screen.SelectedConflict.ShouldBeNull();
    }

    [Theory]
    [InlineData(ConflictChoice.KeepLocal)]
    [InlineData(ConflictChoice.KeepRemote)]
    [InlineData(ConflictChoice.KeepBoth)]
    public async Task resolve_every_ticked_conflict_with_one_choice_in_a_bulk_action(ConflictChoice choice)
    {
        var views = new[] { View(path: "a.txt", age: TimeSpan.FromDays(3)), View(path: "b.txt", age: TimeSpan.FromDays(2)), View(path: "c.txt", age: TimeSpan.FromDays(1)) };
        var (host, screen, connection, client) = Create(views);
        using var lifetime = host;
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(1, [views[2]]));
        screen.Rows[0].IsSelected = true;
        screen.Rows[1].IsSelected = true;

        screen.SelectedCount.ShouldBe(2);
        screen.RequestResolution(choice);
        await screen.ConfirmAsync(Token);

        AssertSent(client, [views[0].ConflictId, views[1].ConflictId], choice);
        connection.Conflicts.ShouldBe([views[2]]);
        screen.SelectedCount.ShouldBe(0);
    }

    [Fact]
    public async Task select_all_and_apply_one_decision_to_every_conflict()
    {
        var views = new[] { View(path: "a.txt"), View(path: "b.txt") };
        var (host, screen, _, client) = Create(views);
        using var lifetime = host;
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(0, []));

        screen.SelectAll();
        screen.RequestResolution(ConflictChoice.KeepBoth);
        await screen.ConfirmAsync(Token);

        client.Sent.Single().ShouldBeOfType<ResolveConflict>().ConflictIds.Count.ShouldBe(2);
        screen.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task keep_a_deferred_conflict_listed_marked_as_deferred()
    {
        var open = View(path: "a.txt");
        var (host, screen, connection, client) = Create(open);
        using var lifetime = host;
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(1, [open with { Status = "Deferred" }]));
        screen.SelectedConflict = screen.Rows[0];

        screen.RequestResolution(ConflictChoice.Defer);
        await screen.ConfirmAsync(Token);

        AssertSent(client, [open.ConflictId], ConflictChoice.Defer);
        connection.Conflicts.Single().Status.ShouldBe("Deferred");
        screen.Rows.Single().IsDeferred.ShouldBeTrue();
        screen.Rows.Single().Status.Text.ShouldBe("Deferred");
    }

    [Fact]
    public async Task resolve_a_deferred_conflict()
    {
        var deferred = View(status: "Deferred");
        var (host, screen, connection, client) = Create(deferred);
        using var lifetime = host;
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(0, []));
        screen.SelectedConflict = screen.Rows[0];

        screen.RequestResolution(ConflictChoice.KeepRemote);
        await screen.ConfirmAsync(Token);

        AssertSent(client, [deferred.ConflictId], ConflictChoice.KeepRemote);
        connection.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task show_the_reason_when_the_daemon_rejects_and_still_show_what_was_resolved()
    {
        var first = View(path: "a.txt", age: TimeSpan.FromDays(2));
        var second = View(path: "b.txt", age: TimeSpan.FromDays(1));
        var (host, screen, _, client) = Create(first, second);
        using var lifetime = host;
        client.CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "That conflict no longer exists."));
        client.ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(1, [second]));
        screen.SelectAll();

        screen.RequestResolution(ConflictChoice.KeepLocal);
        await screen.ConfirmAsync(Token);

        screen.ErrorMessage.ShouldBe("That conflict no longer exists.");
        screen.Rows.Select(row => row.Name).ShouldBe(["b.txt"]);
    }

    [Fact]
    public async Task say_the_daemon_is_unreachable_and_keep_the_list_when_it_cannot_connect()
    {
        var view = View();
        var unreachable = new FakeIpcClient([], available: false);
        using var host = OperationScreensHost.Create(unreachable, new FixedTimeProvider(Now));
        var screen = host.Screen<ConflictsViewModel>();
        host.Services.GetRequiredService<ConnectionViewModel>().ReplaceConflicts(ConflictPages.Of(view));
        screen.SelectedConflict = screen.Rows[0];

        screen.RequestResolution(ConflictChoice.KeepLocal);
        await screen.ConfirmAsync(Token);

        screen.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        screen.Rows.Count.ShouldBe(1);
        unreachable.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task do_nothing_when_confirmed_without_a_pending_request()
    {
        var (host, screen, _, client) = Create(View());
        using var lifetime = host;

        await screen.ConfirmAsync(Token);

        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation_when_confirming()
    {
        var (host, screen, _, client) = Create(View());
        using var lifetime = host;
        screen.SelectedConflict = screen.Rows[0];
        screen.RequestResolution(ConflictChoice.KeepLocal);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.ConfirmAsync(cancelled.Token));

        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task follow_conflict_detected_and_conflict_resolved_live()
    {
        var existing = View(path: "a.txt", age: TimeSpan.FromDays(2));
        var client = new FakeIpcClient([], available: true)
        {
            JobsReply = new Ok<SyncJobsView>(new SyncJobsView([new SyncJobView(Job, Guid.NewGuid(), "OneDrive", "/home/ada/OneDrive", SyncBootstrapMode.Merge)])),
            ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(1, [existing]))
        };
        using var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));
        var connection = new ConnectionViewModel(() => client, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        var screen = new ConflictsViewModel(host.Services.GetRequiredService<ILocalisationService>(), host.Services.GetRequiredService<ICultureService>(), () => client, connection, new FixedTimeProvider(Now));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = connection.RunAsync(source.Token);
        await WaitForAsync(() => screen.Rows.Count == 1);
        screen.SelectedConflict = screen.Rows[0];
        screen.Rows[0].IsSelected = true;
        var detected = View(path: "b.txt", age: TimeSpan.FromHours(1));
        client.Raise(new ConflictDetected(detected));
        await WaitForAsync(() => screen.Rows.Count == 2);

        screen.Rows.Select(row => row.Name).ShouldBe(["a.txt", "b.txt"]);
        screen.SelectedConflict!.Name.ShouldBe("a.txt");
        screen.SelectedCount.ShouldBe(1);

        client.Raise(new ConflictResolved(existing.ConflictId, existing.SyncJobId, existing.ItemId, "KeepLocal", Now));
        await WaitForAsync(() => screen.Rows.Count == 1);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        screen.Rows.Select(row => row.Name).ShouldBe(["b.txt"]);
        screen.SelectedConflict.ShouldBeNull();
        screen.SelectedCount.ShouldBe(0);
    }

    [Fact]
    public void show_the_detail_of_the_conflict_a_deep_link_names()
    {
        var target = View(path: "b.txt");
        var (host, screen, _, _) = Create(View(path: "a.txt", age: TimeSpan.FromDays(3)), target);
        using var lifetime = host;

        screen.OnNavigatedTo(target.ConflictId.ToString());

        screen.SelectedConflict!.Name.ShouldBe("b.txt");
    }

    [Theory]
    [InlineData("nope")]
    [InlineData(null)]
    public void leave_the_selection_alone_for_an_unknown_deep_link_target(string? target)
    {
        var (host, screen, _, _) = Create(View());
        using var lifetime = host;

        screen.OnNavigatedTo(target);

        screen.SelectedConflict.ShouldBeNull();
    }

    private static void AssertSent(FakeIpcClient client, Guid[] ids, ConflictChoice choice)
    {
        var command = client.Sent.ShouldHaveSingleItem().ShouldBeOfType<ResolveConflict>();
        command.ConflictIds.ShouldBe(ids);
        command.Choice.ShouldBe(choice);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(5, Token);

        condition().ShouldBeTrue();
    }

    [Fact]
    public async Task load_the_next_page_of_each_job_after_the_conflicts_shown()
    {
        var first = View(path: "a.txt", age: TimeSpan.FromDays(2));
        var second = View(path: "b.txt", age: TimeSpan.FromDays(1));
        var (host, screen, connection, client) = Create();
        using var lifetime = host;
        connection.ReplaceConflicts(new Dictionary<Guid, ConflictsView> { [Job] = new(2, [first]) });
        var asked = new List<GetConflicts>();
        client.ConflictsReply = query => { asked.Add(query); return new Ok<ConflictsView>(new ConflictsView(2, [second])); };
        screen.CanLoadMore.ShouldBeTrue();

        await screen.LoadMoreCommand.ExecuteAsync(null);

        asked.ShouldHaveSingleItem().Skip.ShouldBe(1);
        screen.Rows.Select(row => row.Name).ShouldBe(["a.txt", "b.txt"]);
        screen.CanLoadMore.ShouldBeFalse();
    }

    [Fact]
    public async Task honour_cancellation_when_loading_more()
    {
        var (host, screen, _, _) = Create(View());
        using var lifetime = host;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.LoadMoreAsync(cancelled.Token));
    }
}

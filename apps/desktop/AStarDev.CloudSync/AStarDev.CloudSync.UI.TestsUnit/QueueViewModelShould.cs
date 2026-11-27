using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class QueueViewModelShould
{
    private static readonly DateTimeOffset Created = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DeadLetterView DeadLetter(string error) => new(Guid.NewGuid(), Guid.NewGuid(), "UploadFile", 5, error, Created);

    private static Result<DeadLettersView, IpcError> Reply(params DeadLetterView[] views) => new Ok<DeadLettersView>(new DeadLettersView(views.Length, views));

    [Fact]
    public void show_a_real_count_per_status_and_the_pending_and_running_operations()
    {
        using var host = OperationScreensHost.Create();
        var queue = host.Screen<QueueViewModel>();
        var running = new QueuedOperationView(Guid.NewGuid(), "OneDrive", "UploadFile", "Running", "a.txt", 1, Created);

        host.Services.GetRequiredService<ConnectionViewModel>().Queue = new QueueView(2, 1, 4, 0, [running]);

        queue.StatusCounts.Select(count => count.Text).ShouldBe(["Pending: 2", "Running: 1", "Completed: 4"]);
        queue.Rows.ShouldHaveSingleItem().Entry.Status.ShouldBe(OperationStatus.Running);
    }

    [Fact]
    public void follow_the_queue_when_it_changes()
    {
        using var host = OperationScreensHost.Create();
        var queue = host.Screen<QueueViewModel>();
        var connection = host.Services.GetRequiredService<ConnectionViewModel>();
        connection.Queue = new QueueView(1, 0, 0, 0, [new QueuedOperationView(Guid.NewGuid(), "OneDrive", "UploadFile", "Pending", "a.txt", 1, Created)]);

        connection.Queue = new QueueView(0, 0, 1, 1, []);

        queue.StatusCounts.Select(count => count.Text).ShouldBe(["Completed: 1", "Failed: 1"]);
        queue.Rows.ShouldBeEmpty();
    }

    [Fact]
    public void show_no_count_before_the_daemon_has_reported()
    {
        using var host = OperationScreensHost.Create();

        host.Screen<QueueViewModel>().StatusCounts.ShouldBeEmpty();
    }

    [Fact]
    public void hide_the_filters_and_point_conflicts_to_their_own_screen()
    {
        using var host = OperationScreensHost.Create();

        var queue = host.Screen<QueueViewModel>();

        queue.ShowTools.ShouldBeFalse();
        queue.ConflictsNote.ShouldNotStartWith("!");
    }

    [Fact]
    public async Task list_dead_letters_with_retry_and_ignore()
    {
        var first = DeadLetter("access denied");
        var second = DeadLetter("corrupt file");
        var client = new FakeIpcClient([], available: true) { DeadLettersReply = Reply(second) };
        using var host = OperationScreensHost.Create(client);
        var queue = host.Screen<QueueViewModel>();
        var connection = host.Services.GetRequiredService<ConnectionViewModel>();

        connection.DeadLetters.Add(first);
        connection.DeadLetters.Add(second);
        var row = queue.DeadLetters.First();
        row.TypeText.ShouldBe("Upload file");
        row.AttemptsText.ShouldBe("Attempts: 5");
        row.ErrorText.ShouldBe("access denied");
        row.DateText.ShouldBe("20/11/2026");
        row.TimeText.ShouldBe("12:00");
        queue.HasDeadLetters.ShouldBeTrue();

        await row.RetryCommand.ExecuteAsync(null);

        client.Sent.ShouldBe([new RetryOperation(first.OperationId)]);
        queue.DeadLetters.Select(item => item.OperationId).ShouldBe([second.OperationId]);

        client.DeadLettersReply = Reply();
        await queue.DeadLetters.Single().IgnoreCommand.ExecuteAsync(null);

        client.Sent.ShouldBe([new RetryOperation(first.OperationId), new IgnoreOperation(second.OperationId)]);
        queue.DeadLetters.ShouldBeEmpty();
        queue.HasDeadLetters.ShouldBeFalse();
        queue.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task show_the_reason_when_the_daemon_rejects_an_action()
    {
        var dead = DeadLetter("access denied");
        var client = new FakeIpcClient([], available: true) { CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "That operation is gone.")), DeadLettersReply = Reply(dead) };
        using var host = OperationScreensHost.Create(client);
        var queue = host.Screen<QueueViewModel>();
        host.Services.GetRequiredService<ConnectionViewModel>().DeadLetters.Add(dead);

        await queue.IgnoreAsync(dead.OperationId, Token);

        queue.ErrorMessage.ShouldBe("That operation is gone.");
        queue.DeadLetters.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task honour_cancellation_of_an_action()
    {
        var dead = DeadLetter("access denied");
        var client = new FakeIpcClient([], available: true);
        using var host = OperationScreensHost.Create(client);
        var queue = host.Screen<QueueViewModel>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => queue.RetryAsync(dead.OperationId, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => queue.IgnoreAsync(dead.OperationId, cancelled.Token));

        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task add_the_next_page_of_dead_letters_below_those_shown()
    {
        var first = DeadLetter("access denied");
        var second = DeadLetter("corrupt file");
        var client = new FakeIpcClient([], available: true) { DeadLettersReply = new Ok<DeadLettersView>(new DeadLettersView(2, [second])) };
        using var host = OperationScreensHost.Create(client);
        var queue = host.Screen<QueueViewModel>();
        host.Services.GetRequiredService<ConnectionViewModel>().ReplaceDeadLetters(new DeadLettersView(2, [first]));
        queue.CanLoadMore.ShouldBeTrue();

        await queue.LoadMoreCommand.ExecuteAsync(null);

        queue.DeadLetters.Select(row => row.OperationId).ShouldBe([first.OperationId, second.OperationId]);
        queue.CanLoadMore.ShouldBeFalse();
        queue.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task honour_cancellation_when_loading_more()
    {
        using var host = OperationScreensHost.Create(new FakeIpcClient([], available: true));
        var queue = host.Screen<QueueViewModel>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => queue.LoadMoreAsync(cancelled.Token));
    }
}

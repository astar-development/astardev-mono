using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ProviderErrorViewModelShould
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task show_the_correlation_id_from_the_daemon()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "Sign-in failed.", TraceId)) };
        using var host = ProviderTestHost.Create(client);

        await host.Connect.ConnectAsync(Token);

        var error = host.Providers.Error.ShouldNotBeNull();
        error.CorrelationText.ShouldContain(TraceId);
        error.CorrelationText.ShouldNotContain("sample");
    }

    [Fact]
    public async Task show_the_error_code_and_link()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Fail<IpcError>(new HandlerFailed("Sign-in failed.", Code("CS-DAEMON-0042"))) };
        using var host = ProviderTestHost.Create(client);

        await host.Connect.ConnectAsync(Token);

        var error = host.Providers.Error.ShouldNotBeNull();
        error.CodeText.ShouldContain("CS-DAEMON-0042");
        error.HelpLink.ShouldBe("https://astar-development.co.uk/errors/CS-DAEMON-0042");
        error.HasLink.ShouldBeTrue();
    }

    [Fact]
    public async Task open_the_link_through_the_launcher()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Fail<IpcError>(new HandlerFailed("Sign-in failed.", Code("CS-DAEMON-0042"))) };
        using var host = ProviderTestHost.Create(client);
        await host.Connect.ConnectAsync(Token);

        await host.Providers.Error.ShouldNotBeNull().OpenLinkAsync(Token);

        host.Launcher.Launched.ShouldHaveSingleItem().AbsoluteUri.ShouldBe("https://astar-development.co.uk/errors/CS-DAEMON-0042");
    }

    [Fact]
    public async Task show_no_link_when_the_daemon_sent_no_code()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "denied")) };
        using var host = ProviderTestHost.Create(client);

        await host.Connect.ConnectAsync(Token);

        var error = host.Providers.Error.ShouldNotBeNull();
        error.HasLink.ShouldBeFalse();
        await error.OpenLinkAsync(Token);
        host.Launcher.Launched.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation_when_opening_the_link()
    {
        var client = new FakeIpcClient([], available: true) { CommandReply = new Fail<IpcError>(new HandlerFailed("Sign-in failed.", Code("CS-DAEMON-0042"))) };
        using var host = ProviderTestHost.Create(client);
        await host.Connect.ConnectAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Providers.Error.ShouldNotBeNull().OpenLinkAsync(cancelled.Token));
    }

    private static ErrorCode Code(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}

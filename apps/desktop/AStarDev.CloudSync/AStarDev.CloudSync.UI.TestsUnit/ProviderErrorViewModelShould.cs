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
}

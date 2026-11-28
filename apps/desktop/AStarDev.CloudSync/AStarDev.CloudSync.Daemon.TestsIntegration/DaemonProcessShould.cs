using System.Diagnostics;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsIntegration;

public sealed class DaemonProcessShould : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("cs-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 75)]
    public async Task exit_with_the_code_the_stop_request_asks_for(bool restart, int expected)
    {
        var socket = Path.Combine(directory, "d.sock");
        using var process = Start(socket);
        await using var client = UnixSocketIpc.CreateClient(socket);
        await ConnectAsync(client);

        var reply = await client.SendAsync(new StopDaemon(restart), Token);
        await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(30), Token);

        reply.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        process.ExitCode.ShouldBe(expected);
    }

    private Process Start(string socket)
    {
        var info = new ProcessStartInfo("dotnet", Path.Combine(AppContext.BaseDirectory, "AStarDev.CloudSync.Daemon.dll")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["Ipc__SocketPath"] = socket;
        info.Environment["Database__Path"] = Path.Combine(directory, "cloudsync.db");
        info.Environment["Telemetry__Database__Path"] = Path.Combine(directory, "telemetry.db");

        return Process.Start(info) ?? throw new InvalidOperationException("The daemon process did not start.");
    }

    private static async Task ConnectAsync(IIpcClient client)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if ((await client.ConnectAsync(Token)).Value is Ok<Unit>) return;

            await Task.Delay(100, Token);
        }

        throw new TimeoutException("The daemon did not accept connections.");
    }
}

using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.IPC.TestsUnit;

[Collection("IPC metrics")]
public sealed class IpcLoggingShould
{
    private sealed record FailingQuery : IQuery<DaemonStatusView>;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task log_a_failed_request_as_a_warning_with_structured_fields()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-ipc-logging-{Guid.NewGuid():N}.sock");
        var logger = new RecordingLogger();
        await using var server = UnixSocketIpc.CreateServer(socketPath, logger: logger);
        await using var client = UnixSocketIpc.CreateClient(socketPath);
        server.MapQuery((FailingQuery _, CancellationToken _) => Task.FromResult<Result<DaemonStatusView, IpcError>>(new Fail<IpcError>(new HandlerFailed("nope"))));
        (await server.StartAsync(Token)).ShouldSucceed();
        (await client.ConnectAsync(Token)).ShouldSucceed();

        await client.QueryAsync(new FailingQuery(), Token);

        var failure = logger.Entries.Single(entry => entry.EventId == 2002);
        failure.Level.ShouldBe(LogLevel.Warning);
        failure.State["MessageType"].ShouldBe(nameof(FailingQuery));
        failure.State["Side"].ShouldBe("server");
        File.Delete(socketPath);
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<(LogLevel Level, int EventId, Dictionary<string, object?> State)> entries = [];

        public IReadOnlyList<(LogLevel Level, int EventId, Dictionary<string, object?> State)> Entries
        {
            get
            {
                lock (entries) return [.. entries];
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value) : [];
            lock (entries) entries.Add((logLevel, eventId.Id, values));
        }
    }
}

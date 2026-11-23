using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class OperationSchedulerShould
{
    [Fact]
    public async Task log_through_the_generated_messages()
    {
        var logger = new RecordingLogger<OperationScheduler>();
        await using var scheduler = new OperationScheduler(new FailingOperationStore(), new NoOperationExecutor(), Options.Create(new OperationSchedulerOptions { PollInterval = TimeSpan.FromMinutes(1) }), TimeProvider.System, logger);

        scheduler.Start();
        await WaitForAsync(logger, TestContext.Current.CancellationToken);

        logger.Entries.ShouldContain(entry => entry.EventId == 1203 && entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task include_the_error_code_when_logging_a_failure()
    {
        var logger = new RecordingLogger<OperationScheduler>();
        await using var scheduler = new OperationScheduler(new FailingOperationStore(), new NoOperationExecutor(), Options.Create(new OperationSchedulerOptions { PollInterval = TimeSpan.FromMinutes(1) }), TimeProvider.System, logger);

        scheduler.Start();
        await WaitForAsync(logger, TestContext.Current.CancellationToken);

        logger.Entries.ShouldContain(entry => entry.EventId == 1203 && entry.Message.Contains("CS-PORTS-0008", StringComparison.Ordinal));
    }

    private static async Task WaitForAsync(RecordingLogger<OperationScheduler> logger, CancellationToken cancellationToken)
    {
        while (logger.Entries.Count == 0) await Task.Delay(10, cancellationToken);
    }

    private sealed class NoOperationExecutor : IOperationExecutor
    {
        public Task<Result<Unit, ProviderError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

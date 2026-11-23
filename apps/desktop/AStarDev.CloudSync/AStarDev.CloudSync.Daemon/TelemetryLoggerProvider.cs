using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Hands every log message to the <see cref="ITelemetryWriter"/>, which batches them into the telemetry store, so the Logs screen can show them after a restart.
/// Writing never blocks and never fails the caller: a full queue or a failing store drops the entry. Messages are scrubbed by <see cref="LogRedaction"/>.
/// Entity Framework categories and the batch writer's own category are not stored, because storing a batch would log, and that log would be stored in turn.
/// The writer is resolved on first use, because it logs itself and so cannot be a dependency of a logger provider.
/// </summary>
internal sealed class TelemetryLoggerProvider(IServiceProvider services, TimeProvider timeProvider) : ILoggerProvider
{
    private readonly Lazy<ITelemetryWriter> writer = new(services.GetRequiredService<ITelemetryWriter>);

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => IsStored(categoryName) ? new TelemetryLogger(categoryName, writer, timeProvider) : NullLogger.Instance;

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private static bool IsStored(string category)
        => !category.StartsWith(ApplicationConstants.EntityFrameworkCategoryPrefix, StringComparison.Ordinal) && category != ApplicationConstants.TelemetryBatchWriterCategory;

    private sealed class TelemetryLogger(string category, Lazy<ITelemetryWriter> writer, TimeProvider timeProvider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            writer.Value.TryWrite(new TelemetryEntry(TelemetryKind.Log, timeProvider.GetUtcNow(), (int)logLevel, category, LogRedaction.Scrub(formatter(state, exception))));
        }
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The span and metrics recorded around every save. Collected by name, so Persistence needs no reference to Observability.</summary>
internal static class PersistenceTelemetry
{
    private static readonly ActivitySource Source = new(ApplicationConstants.TelemetryName);
    private static readonly Meter Meter = new(ApplicationConstants.TelemetryName);
    private static readonly Counter<long> Saves = Meter.CreateCounter<long>("cloudsync.persistence.saves", "{save}", "Saves sent to the database, successful or not.");
    private static readonly Histogram<double> SaveDuration = Meter.CreateHistogram<double>("cloudsync.persistence.save.duration", "s", "How long a save took.");

    /// <summary>Runs <paramref name="save"/> inside a <c>persistence.save</c> span and records the count and duration.</summary>
    /// <param name="entries">How many tracked entities the save will write.</param>
    /// <param name="save">The save to run.</param>
    /// <returns>What <paramref name="save"/> returned. A failure is recorded on the span and rethrown unchanged, because <c>DbContext</c> reports failures by exception.</returns>
    public static int Record(int entries, Func<int> save)
    {
        using var activity = Start(entries);
        var started = Stopwatch.GetTimestamp();
        var outcome = Try.Run(save);

        return Finish(activity, started, outcome);
    }

    /// <summary>Asynchronous <see cref="Record"/>. Cancellation propagates and is not recorded as an error.</summary>
    /// <param name="entries">How many tracked entities the save will write.</param>
    /// <param name="save">The save to run; it receives <paramref name="cancellationToken"/>.</param>
    /// <param name="cancellationToken">Passed to <paramref name="save"/>.</param>
    /// <returns>What <paramref name="save"/> returned.</returns>
    public static async Task<int> RecordAsync(int entries, Func<CancellationToken, Task<int>> save, CancellationToken cancellationToken)
    {
        using var activity = Start(entries);
        var started = Stopwatch.GetTimestamp();
        var outcome = await Try.RunAsync(save, cancellationToken).ConfigureAwait(false);

        return Finish(activity, started, outcome);
    }

    private static Activity? Start(int entries)
    {
        var activity = Source.StartActivity("persistence.save", ActivityKind.Client);
        activity?.SetTag("db.system.name", ApplicationConstants.SqliteSystemName);
        activity?.SetTag("cloudsync.persistence.entries", entries);

        return activity;
    }

    private static int Finish(Activity? activity, long started, Exceptional<int> outcome)
    {
        Saves.Add(1);
        SaveDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds);

        return outcome
            .TapError(exception => activity?.SetStatus(ActivityStatusCode.Error, exception.Message))
            .GetOrThrow();
    }
}

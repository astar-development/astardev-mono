using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class QueueMetricsShould
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task report_queue_depth_and_operation_duration()
    {
        var executor = new ScriptedOperationExecutor(operation => operation.Priority == OperationPriority.UserInitiated ? new Fail<ProviderError>(new Permanent("forbidden")) : new Ok<Unit>(default));
        await using var host = DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new ManualTimeProvider(Start));
            services.AddSingleton<IOperationExecutor>(executor);
            services.AddSyncEngine();
        });
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Start);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, Start, Start);
        await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token);
        await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token);
        var store = host.Services.GetRequiredService<IOperationStore>();
        await store.EnqueueAsync(Queued(job.Id, OperationPriority.UserInitiated, 0), Token);
        await store.EnqueueAsync(Queued(job.Id, OperationPriority.Background, 1), Token);
        await store.EnqueueAsync(Queued(job.Id, OperationPriority.Background, 2), Token);
        var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        var gauges = new Dictionary<string, long>();
        var durations = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name.StartsWith("AStarDev.CloudSync.", StringComparison.Ordinal)) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => gauges[instrument.Name] = value);
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => durations.Add(value));
        listener.Start();

        (await scheduler.RunOnceAsync(Token)).Value.ShouldBeOfType<Ok<bool>>();
        listener.RecordObservableInstruments();

        gauges["cloudsync.queue.pending"].ShouldBe(2);
        gauges["cloudsync.queue.running"].ShouldBe(0);
        gauges["cloudsync.queue.dead_lettered"].ShouldBe(1);
        durations.Count.ShouldBe(1);
    }

    private static Operation Queued(SyncJobId job, OperationPriority priority, int ageRank)
        => new(new OperationId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), OperationType.UploadFile, priority, OperationStatus.Pending, 0, Start, null, Start.AddMinutes(ageRank));
}

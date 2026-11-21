using System.Diagnostics;
using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class PersistenceTelemetryShould
{
    private const string SourceName = "AStarDev.CloudSync.Persistence";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task record_a_span_per_save()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var factory = host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>();
        var spans = new List<Activity>();
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        using var listener = ListenToChildrenOf(testSource, spans);
        using var testRoot = testSource.StartActivity("test");

        await using (var first = await factory.CreateDbContextAsync(Token))
        {
            first.Accounts.Add(NewAccount("acct-1"));
            await first.SaveChangesAsync(Token);
        }

        await using (var second = await factory.CreateDbContextAsync(Token))
        {
            second.Accounts.Add(NewAccount("acct-2"));
            await second.SaveChangesAsync(Token);
        }

        spans.Count.ShouldBe(2);
        spans.ShouldAllBe(span => span.OperationName == "persistence.save");
        spans.ShouldAllBe(span => (string?)span.GetTagItem("db.system.name") == "sqlite");
        spans.ShouldAllBe(span => (int)span.GetTagItem("cloudsync.persistence.entries")! == 1);
    }

    [Fact]
    public async Task mark_the_span_as_failed_when_a_save_fails()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var factory = host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>();
        var spans = new List<Activity>();
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        using var listener = ListenToChildrenOf(testSource, spans);
        using var testRoot = testSource.StartActivity("test");
        await using var context = await factory.CreateDbContextAsync(Token);
        context.Accounts.Add(NewAccount("acct-1"));
        context.Accounts.Add(NewAccount("acct-1"));

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Token));

        spans.Single().Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task count_each_save_and_time_it()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var factory = host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>();
        var measurements = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == SourceName) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.Start();

        await using var context = await factory.CreateDbContextAsync(Token);
        context.Accounts.Add(NewAccount("acct-1"));
        await context.SaveChangesAsync(Token);

        measurements.ShouldContain("cloudsync.persistence.saves");
        measurements.ShouldContain("cloudsync.persistence.save.duration");
    }

    private static ActivityListener ListenToChildrenOf(ActivitySource testSource, List<Activity> spans)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName || source == testSource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.Source.Name == SourceName && activity.Parent?.Source == testSource) spans.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static AccountEntity NewAccount(string identifier)
        => new() { Id = new AccountId(Guid.NewGuid()), ProviderType = "OneDrive", DisplayName = identifier, AccountIdentifier = identifier, CreatedUtc = DateTimeOffset.UtcNow };
}

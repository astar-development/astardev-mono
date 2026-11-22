using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ActivitiesQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task answer_with_the_activity_reported_by_the_crawl()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = new SyncJobId(Guid.NewGuid());

        await host.Services.GetRequiredService<ICrawlProgressSink>().ReportAsync(new CrawlProgress(job, new RemoteItemId("docs"), 40, false, 100), Token);
        var result = await host.Services.GetRequiredService<ActivitiesQuery>().HandleAsync(new GetActivities(), Token);

        var activity = result.Value.ShouldBeOfType<Ok<ActivitiesView>>().Value.Activities.ShouldHaveSingleItem();
        activity.SyncJobId.ShouldBe(job.Value);
        activity.Percent.ShouldBe(40);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ActivitiesQuery>().HandleAsync(new GetActivities(), cancelled.Token));
    }
}

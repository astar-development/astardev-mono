using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class DeltaErrorShould
{
    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        IErrorInfo[] cases =
        [
            new ScopeNotReady(new SyncScope(new SyncJobId(Guid.NewGuid()), new RemoteItemId("root"))),
            new DeltaProviderFailed(new Transient("net")),
            new DeltaStoreFailed(new StorageFailed("disk")),
            new ReindexFailed(new SyncScope(new SyncJobId(Guid.NewGuid()), new RemoteItemId("root")))
        ];

        cases.ShouldAllBe(error => error.Code.Value.StartsWith("CS-SYNC-") && error.HelpLink == ErrorLinks.Root + error.Code.Value);
        cases.Select(error => error.Code.Value).Distinct().Count().ShouldBe(cases.Length);
    }
}

using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class LocalObservationErrorShould
{
    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        IErrorInfo[] cases =
        [
            new FileSystemFailed("gone"),
            new ObservationStoreFailed(new StorageFailed("disk"))
        ];

        cases.ShouldAllBe(error => error.Code.Value.StartsWith("CS-SYNC-") && error.HelpLink == ErrorLinks.Root + error.Code.Value);
        cases.Select(error => error.Code.Value).Distinct().Count().ShouldBe(cases.Length);
    }
}

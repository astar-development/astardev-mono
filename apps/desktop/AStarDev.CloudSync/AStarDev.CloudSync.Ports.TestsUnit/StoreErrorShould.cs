using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports.TestsUnit;

public sealed class StoreErrorShould
{
    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        var jobId = new SyncJobId(Guid.NewGuid());
        var scope = new SyncScope(jobId, new RemoteItemId("root"));
        IErrorInfo[] cases =
        [
            new AccountNotFound(new AccountId(Guid.NewGuid())),
            new AccountAlreadyExists(new AccountId(Guid.NewGuid())),
            new StorageFailed("locked"),
            new SyncJobNotFound(jobId),
            new SyncJobAlreadyExists(jobId),
            new SyncScopeAlreadyExists(scope),
            new SyncScopeNotFound(scope),
            new OperationNotRunning(new OperationId(Guid.NewGuid())),
            new OperationNotDeadLettered(new OperationId(Guid.NewGuid())),
            new ConflictNotFound(new ConflictId(Guid.NewGuid())),
            new UnknownSetting("x"),
            new InvalidSettingValue("x", "y")
        ];

        cases.ShouldExposeUniqueCodesAndLinks();
    }

    [Fact]
    public void not_share_a_code_with_a_provider_error()
    {
        IErrorInfo[] store = [new StorageFailed("a"), new UnknownSetting("x")];
        IErrorInfo[] provider = [new Transient("a"), new Permanent("b")];

        store.Concat(provider).ShouldExposeUniqueCodesAndLinks();
    }
}

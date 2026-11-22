namespace AStarDev.CloudSync.Observability.TestsUnit;

public sealed class CorrelationScopeShould
{
    [Fact]
    public void include_the_correlation_ids_in_log_state()
    {
        var logger = new CapturingLogger();

        using var scope = CorrelationScope.Begin(logger, operationId: "op-1", itemId: "item-1", syncJobId: "job-1", accountId: "account-1", remoteItemId: "remote-1");

        var state = logger.Scopes.ShouldHaveSingleItem().ShouldBeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().ShouldNotBeNull();
        state.ToDictionary(pair => pair.Key, pair => pair.Value).ShouldBe(new Dictionary<string, object?>
        {
            ["OperationId"] = "op-1",
            ["ItemId"] = "item-1",
            ["SyncJobId"] = "job-1",
            ["AccountId"] = "account-1",
            ["RemoteItemId"] = "remote-1"
        });
    }

    [Fact]
    public void leave_out_the_ids_that_are_not_available()
    {
        var logger = new CapturingLogger();

        using var scope = CorrelationScope.Begin(logger, syncJobId: "job-1");

        var state = logger.Scopes.ShouldHaveSingleItem().ShouldBeAssignableTo<IReadOnlyList<KeyValuePair<string, object?>>>().ShouldNotBeNull();
        state.ShouldHaveSingleItem().ShouldBe(new KeyValuePair<string, object?>("SyncJobId", "job-1"));
    }

    [Fact]
    public void end_the_scope_when_disposed()
    {
        var logger = new CapturingLogger();

        CorrelationScope.Begin(logger, operationId: "op-1").Dispose();

        logger.DisposedScopes.ShouldBe(1);
    }
}

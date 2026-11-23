using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class DatabaseErrorShould
{
    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        IErrorInfo[] cases = [new DatabaseUnavailable("locked"), new MigrationFailed("bad schema")];

        cases.ShouldAllBe(error => error.Code.Value.StartsWith("CS-DB-") && error.HelpLink == ErrorLinks.Root + error.Code.Value);
        cases.Select(error => error.Code.Value).Distinct().Count().ShouldBe(cases.Length);
    }
}

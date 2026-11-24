using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Domain.TestsUnit;

public sealed class ScopeCoverageShould
{
    private static readonly SyncJobId Job = new(Guid.NewGuid());

    private static SyncScope Scope(string id, string path) => new(Job, new RemoteItemId(id), new RelativePath(path));

    [Fact]
    public void cover_nothing_without_a_scope()
        => ScopeCoverage.Covers([], new RelativePath("a.txt")).ShouldBeFalse();

    [Fact]
    public void cover_everything_for_the_drive_root_alias()
        => ScopeCoverage.Covers([Scope("root", string.Empty)], new RelativePath("any/where.txt")).ShouldBeTrue();

    [Theory]
    [InlineData("Docs", "Docs", true)]
    [InlineData("Docs", "Docs/a.txt", true)]
    [InlineData("Docs", "Docs/sub/a.txt", true)]
    [InlineData("Docs", "Docs2/a.txt", false)]
    [InlineData("Docs", "docs/a.txt", false)]
    [InlineData("Docs/Clients", "Docs/Other/a.txt", false)]
    [InlineData("Docs/Clients", "Docs", false)]
    public void cover_only_the_scope_folder_and_what_is_below_it(string scopePath, string path, bool expected)
        => ScopeCoverage.Covers([Scope("folder", scopePath)], new RelativePath(path)).ShouldBe(expected);

    [Fact]
    public void cover_nothing_for_a_scope_whose_path_is_unknown()
        => ScopeCoverage.Covers([new SyncScope(Job, new RemoteItemId("folder"))], new RelativePath("a.txt")).ShouldBeFalse();

    [Theory]
    [InlineData("Docs", true)]
    [InlineData("Docs/Clients", true)]
    [InlineData("Other", false)]
    public void touch_a_folder_that_holds_a_scope(string folder, bool expected)
        => ScopeCoverage.Touches([Scope("folder", "Docs/Clients")], new RelativePath(folder)).ShouldBe(expected);
}

using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class KeepBothShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 19, 8, 14, 17, TimeSpan.Zero);

    [Fact]
    public void name_the_copy_with_date_and_time()
    {
        var copy = KeepBoth.NameCopy(new RelativePath("Report.xlsx"), Stamp, _ => false);

        copy.ShouldBe(new RelativePath("Report (Conflict - 2026-11-19 081417).xlsx"));
    }

    [Fact]
    public void add_a_numeric_suffix_when_the_name_exists()
    {
        var first = new RelativePath("docs/Report (Conflict - 2026-11-19 081417).xlsx");
        var second = new RelativePath("docs/Report (Conflict - 2026-11-19 081417) (2).xlsx");

        var copy = KeepBoth.NameCopy(new RelativePath("docs/Report.xlsx"), Stamp, path => path == first || path == second);

        copy.ShouldBe(new RelativePath("docs/Report (Conflict - 2026-11-19 081417) (3).xlsx"));
    }

    [Fact]
    public void keep_the_folder_and_handle_a_name_without_an_extension()
    {
        var copy = KeepBoth.NameCopy(new RelativePath("a/b/Makefile"), Stamp, _ => false);

        copy.ShouldBe(new RelativePath("a/b/Makefile (Conflict - 2026-11-19 081417)"));
    }

    [Fact]
    public void convert_the_time_to_utc()
    {
        var copy = KeepBoth.NameCopy(new RelativePath("a.txt"), new DateTimeOffset(2026, 11, 19, 9, 14, 17, TimeSpan.FromHours(1)), _ => false);

        copy.ShouldBe(new RelativePath("a (Conflict - 2026-11-19 081417).txt"));
    }
}

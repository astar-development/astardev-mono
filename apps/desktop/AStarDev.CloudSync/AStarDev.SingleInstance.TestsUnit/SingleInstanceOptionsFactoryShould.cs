using Testably.Abstractions;

namespace AStarDev.SingleInstance.TestsUnit;

public sealed class SingleInstanceOptionsFactoryShould
{
    private static readonly System.IO.Abstractions.IDirectoryInfo Folder = new RealFileSystem().DirectoryInfo.New("/tmp/jane.doe@example.com");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void use_the_default_name_when_the_name_is_blank(string? name)
    {
        var options = SingleInstanceOptionsFactory.Create(Folder, name, null);

        options.Name.ShouldBe(SingleInstanceOptionsFactory.DefaultName);
    }

    [Fact]
    public void trim_a_given_name()
    {
        var options = SingleInstanceOptionsFactory.Create(Folder, "  cloudsync ", null);

        options.Name.ShouldBe("cloudsync");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void use_the_default_timeout_when_the_timeout_is_missing_or_not_positive(int? milliseconds)
    {
        var options = SingleInstanceOptionsFactory.Create(Folder, "app", milliseconds is { } value ? TimeSpan.FromMilliseconds(value) : null);

        options.HandOffTimeout.ShouldBe(SingleInstanceOptionsFactory.DefaultHandOffTimeout);
    }

    [Fact]
    public void keep_a_positive_timeout()
    {
        var options = SingleInstanceOptionsFactory.Create(Folder, "app", TimeSpan.FromMilliseconds(750));

        options.HandOffTimeout.ShouldBe(TimeSpan.FromMilliseconds(750));
    }

    [Fact]
    public void create_options_for_a_folder_path() => SingleInstanceOptionsFactory.Create("/tmp/si-path", "app", null).DataFolder.FullName.ShouldBe("/tmp/si-path");

    [Fact]
    public void redact_the_folder_when_printed()
    {
        var options = SingleInstanceOptionsFactory.Create(Folder, "app", null);

        options.ToString().ShouldNotContain("jane.doe");
    }
}

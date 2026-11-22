using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class UnixLocalIdentityReaderShould : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("cloudsync-identity-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void keep_the_identity_when_a_file_is_renamed()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Filesystem identity is read on Linux only.");
        var original = Path.Combine(root, "a.txt");
        File.WriteAllText(original, "abc");
        var reader = new UnixLocalIdentityReader();
        var before = reader.Read(original);

        var moved = Path.Combine(root, "b.txt");
        File.Move(original, moved);

        before.ShouldNotBeNull();
        reader.Read(moved).ShouldBe(before);
    }

    [Fact]
    public void give_two_files_different_identities()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Filesystem identity is read on Linux only.");
        var first = Path.Combine(root, "a.txt");
        var second = Path.Combine(root, "b.txt");
        File.WriteAllText(first, "abc");
        File.WriteAllText(second, "abc");
        var reader = new UnixLocalIdentityReader();

        reader.Read(first).ShouldNotBe(reader.Read(second));
    }

    [Fact]
    public void return_no_identity_for_a_missing_file()
        => new UnixLocalIdentityReader().Read(Path.Combine(root, "missing.txt")).ShouldBeNull();
}

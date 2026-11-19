using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class FunctionalParadigmArchitectureTestsShouldEnsure
{
    private static readonly string[] ReferencedAssemblyNames =
        [.. typeof(Try).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!)];

    [Fact]
    public void the_functional_paradigm_library_references_no_cloudsync_project()
        => ReferencedAssemblyNames.ShouldNotContain(name => name.StartsWith("AStarDev.CloudSync", StringComparison.Ordinal));

    [Fact]
    public void the_functional_paradigm_library_references_no_package_outside_the_bcl()
        => ReferencedAssemblyNames.ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal));
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationServiceCollectionExtensionsShould
{
    [Fact]
    public void return_the_same_collection_so_the_call_can_be_chained()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddClassification().ShouldBeSameAs(builder.Services);
    }

    [Fact]
    public void be_safe_to_call_twice()
    {
        var builder = Host.CreateApplicationBuilder();
        var before = builder.Services.Count;

        builder.Services.AddClassification().AddClassification();

        builder.Services.Count.ShouldBe(before);
    }
}

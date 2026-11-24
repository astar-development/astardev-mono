using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationServiceCollectionExtensionsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task register_the_startup_the_daemon_runs()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);

        host.Services.GetRequiredService<IClassificationStartup>().ShouldNotBeNull();
    }

    [Fact]
    public async Task seed_the_vocabulary_when_the_startup_runs()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);

        await host.Services.GetRequiredService<IClassificationStartup>().RunAsync(Token);

        (await host.Services.GetRequiredService<IClassificationVocabularyProvider>().GetAsync(Token)).Value.ShouldBeOfType<AStarDev.FunctionalParadigm.Ok<ClassificationVocabulary>>().Value.Terms.ShouldNotBeEmpty();
    }
}

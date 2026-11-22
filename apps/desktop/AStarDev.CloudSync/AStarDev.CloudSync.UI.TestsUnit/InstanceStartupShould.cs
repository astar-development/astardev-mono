using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class InstanceStartupShould : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"ui-{Guid.NewGuid():N}"[..11]);
    private readonly ServiceProvider provider = new ServiceCollection().AddLogging().AddSingleInstance().BuildServiceProvider();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<InstanceClaim> ClaimAsync(string path) => provider.GetRequiredService<ISingleInstanceGuard>().ClaimAsync(SingleInstanceOptionsFactory.Create(path, "ui", TimeSpan.FromMilliseconds(500)), Token);

    [Fact]
    public async Task keep_starting_for_the_running_instance()
    {
        var claim = await ClaimAsync(folder);

        await using var primary = claim.ShouldBeOfType<InstanceClaim.Primary>().Instance;
        InstanceStartup.ExitCodeFor(claim).Value.ShouldBeOfType<None<int>>();
    }

    [Fact]
    public async Task exit_successfully_when_another_instance_took_over()
    {
        await using var primary = (await ClaimAsync(folder)).ShouldBeOfType<InstanceClaim.Primary>().Instance;

        var second = await ClaimAsync(folder);

        InstanceStartup.ExitCodeFor(second).Value.ShouldBeOfType<Some<int>>().Value.ShouldBe(0);
    }

    [Fact]
    public async Task fail_when_single_instance_cannot_be_enforced()
    {
        Directory.CreateDirectory(folder);
        var blocker = Path.Combine(folder, "blocker");
        await File.WriteAllTextAsync(blocker, "file", Token);

        var claim = await ClaimAsync(Path.Combine(blocker, "data"));

        InstanceStartup.ExitCodeFor(claim).Value.ShouldBeOfType<Some<int>>().Value.ShouldBe(1);
    }

    public void Dispose()
    {
        provider.Dispose();
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }
}

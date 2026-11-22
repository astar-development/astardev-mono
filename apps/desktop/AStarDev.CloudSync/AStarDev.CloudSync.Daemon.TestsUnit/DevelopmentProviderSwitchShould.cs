using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DevelopmentProviderSwitchShould
{
    private const string FakeProviderAssembly = "AStarDev.CloudSync.FakeProvider";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void reference_the_fake_provider_in_debug_builds_only()
    {
        var referenced = typeof(IpcOptions).Assembly.GetReferencedAssemblies().Any(name => name.Name == FakeProviderAssembly);

#if DEBUG
        referenced.ShouldBeTrue();
#else
        referenced.ShouldBeFalse();
#endif
    }

#if DEBUG
    [Fact]
    public async Task keep_onedrive_when_no_provider_is_configured()
    {
        await using var host = DaemonTestHost.Create();

        host.Services.GetRequiredService<ICloudProvider>().GetType().FullName.ShouldBe("AStarDev.CloudSync.OneDrive.OneDriveProvider");
    }

    [Fact]
    public async Task keep_onedrive_when_onedrive_is_configured()
    {
        await using var host = DaemonTestHost.Create(configuration: Setting("CloudSync:Provider", "OneDrive"));

        host.Services.GetRequiredService<ICloudProvider>().GetType().FullName.ShouldBe("AStarDev.CloudSync.OneDrive.OneDriveProvider");
    }

    [Fact]
    public async Task select_the_fake_provider_with_a_generated_tree_of_the_configured_size()
    {
        await using var host = DaemonTestHost.Create(configuration: new Dictionary<string, string?> { ["CloudSync:Provider"] = "Fake", ["CloudSync:FakeTreeSize"] = "25" });

        var provider = host.Services.GetRequiredService<ICloudProvider>();
        var page = (await provider.GetSnapshotAsync(new RemoteItemId("root"), null, Token)).Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value;

        provider.GetType().FullName.ShouldBe("AStarDev.CloudSync.FakeProvider.FakeCloudProvider");
        page.TotalItems.ShouldBe(25);
    }

    [Fact]
    public async Task serve_content_for_the_generated_files_so_downloads_succeed()
    {
        await using var host = DaemonTestHost.Create(configuration: new Dictionary<string, string?> { ["CloudSync:Provider"] = "Fake", ["CloudSync:FakeTreeSize"] = "5" });

        var download = await host.Services.GetRequiredService<ICloudProvider>().DownloadAsync(new RemoteItemId("item-1"), Token);

        download.Value.ShouldBeOfType<Ok<Stream>>();
    }

    [Fact]
    public async Task let_a_test_replace_the_provider_after_the_composition()
    {
        var replacement = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(replacement);

        host.Services.GetRequiredService<ICloudProvider>().ShouldBeSameAs(replacement);
    }

    private static Dictionary<string, string?> Setting(string key, string value) => new() { [key] = value };
#endif
}

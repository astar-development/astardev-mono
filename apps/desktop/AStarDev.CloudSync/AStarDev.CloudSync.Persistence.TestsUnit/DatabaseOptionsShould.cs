using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class DatabaseOptionsShould
{
    [Fact]
    public void default_the_path_to_the_xdg_data_home()
        => DatabaseOptions.DefaultPath("/data", "/home/u").ShouldBe("/data/astardev-cloudsync/cloudsync.db");

    [Fact]
    public void fall_back_to_the_local_share_folder_when_xdg_data_home_is_not_set()
    {
        DatabaseOptions.DefaultPath(null, "/home/u").ShouldBe("/home/u/.local/share/astardev-cloudsync/cloudsync.db");
        DatabaseOptions.DefaultPath(" ", "/home/u").ShouldBe("/home/u/.local/share/astardev-cloudsync/cloudsync.db");
    }

    [Fact]
    public async Task bind_the_configured_path()
    {
        await using var host = PersistenceTestHost.Create();

        var options = host.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        options.Path.ShouldBe(host.DatabasePath);
    }
}

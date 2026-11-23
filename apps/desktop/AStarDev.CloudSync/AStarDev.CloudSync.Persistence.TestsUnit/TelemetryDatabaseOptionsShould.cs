using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class TelemetryDatabaseOptionsShould
{
    [Fact]
    public void default_to_a_file_beside_the_sync_database()
    {
        var sync = DatabaseOptions.DefaultPath("/data", "/home/u");
        var telemetry = TelemetryDatabaseOptions.DefaultPath("/data", "/home/u");

        telemetry.ShouldBe("/data/astardev-cloudsync/telemetry.db");
        Path.GetDirectoryName(telemetry).ShouldBe(Path.GetDirectoryName(sync));
        telemetry.ShouldNotBe(sync);
    }

    [Fact]
    public void fall_back_to_the_local_share_folder_when_xdg_data_home_is_not_set()
        => TelemetryDatabaseOptions.DefaultPath(" ", "/home/u").ShouldBe("/home/u/.local/share/astardev-cloudsync/telemetry.db");

    [Fact]
    public async Task bind_the_configured_path()
    {
        await using var host = PersistenceTestHost.Create();

        var options = host.Services.GetRequiredService<IOptions<TelemetryDatabaseOptions>>().Value;

        options.Path.ShouldBe(Path.Combine(Path.GetDirectoryName(host.DatabasePath)!, "telemetry.db"));
    }
}

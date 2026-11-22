namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class DaemonSocketPathShould
{
    [Fact]
    public void default_to_the_xdg_runtime_dir()
        => DaemonSocketPath.Resolve("/run/user/1000").ShouldBe("/run/user/1000/astardev-cloudsync/daemon.sock");

    [Fact]
    public void keep_the_instance_folder_beside_the_daemon_folder_under_the_xdg_runtime_dir()
        => DaemonSocketPath.InstanceFolder("/run/user/1000").ShouldBe("/run/user/1000/astardev-cloudsync-ui");

    [Fact]
    public void fall_back_to_a_per_user_temp_directory_without_an_xdg_runtime_dir()
    {
        var path = DaemonSocketPath.Resolve(null);

        path.ShouldStartWith(Path.GetTempPath());
        path.ShouldContain(Environment.UserName);
    }
}

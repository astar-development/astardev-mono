namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class IpcOptionsShould
{
    [Fact]
    public void default_the_socket_path_to_the_xdg_runtime_dir()
        => IpcOptions.DefaultSocketPath("/run/user/1000").ShouldBe("/run/user/1000/astardev-cloudsync/daemon.sock");

    [Fact]
    public void fall_back_to_a_per_user_temp_directory_without_an_xdg_runtime_dir()
    {
        var path = IpcOptions.DefaultSocketPath(null);

        path.ShouldStartWith(Path.GetTempPath());
        path.ShouldContain(Environment.UserName);
    }
}

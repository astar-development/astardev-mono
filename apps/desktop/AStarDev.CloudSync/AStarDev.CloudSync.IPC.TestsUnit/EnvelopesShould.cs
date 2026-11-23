namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class EnvelopesShould
{
    [Fact]
    public void set_the_help_link_on_a_malformed_envelope()
    {
        var error = IpcJson.Decode("null"u8.ToArray()).ShouldFail().Value.ShouldBeOfType<MalformedFrame>();

        error.HelpLink.ShouldBe("https://astar-development.co.uk/errors/CS-IPC-0005");
    }
}

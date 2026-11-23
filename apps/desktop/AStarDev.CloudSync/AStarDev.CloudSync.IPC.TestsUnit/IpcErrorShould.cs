namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class IpcErrorShould
{
    private static IpcError[] Cases =>
    [
        new ConnectionFailed("a"), new NotConnected(), new ConnectionLost("b"), new FrameTooLarge(2, 1), new MalformedFrame("c"), new HandlerFailed("d"), new ListenFailed("e")
    ];

    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        foreach (var error in Cases)
        {
            var info = error.Info();

            info.Code.Value.ShouldStartWith("CS-IPC-");
            info.HelpLink.ShouldBe("https://astar-development.co.uk/errors/" + info.Code.Value);
        }
    }

    [Fact]
    public void give_every_case_its_own_code() =>
        Cases.Select(error => error.Info().Code.Value).Distinct().Count().ShouldBe(Cases.Length);
}

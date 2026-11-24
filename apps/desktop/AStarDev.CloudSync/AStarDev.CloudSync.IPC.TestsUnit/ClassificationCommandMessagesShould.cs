namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class ClassificationCommandMessagesShould
{
    [Fact]
    public void round_trip_every_classification_command()
    {
        var id = Guid.NewGuid();
        var category = Guid.NewGuid();

        RoundTrip(new AssignTerm(id, category));
        RoundTrip(new PromoteSuggestion("zorblax", category));
        RoundTrip(new IgnoreSuggestion("zorblax", category));
        RoundTrip(new CreateCategory("Colour"));
        RoundTrip(new RenameCategory(category, "Colours"));
        RoundTrip(new DeleteCategory(category));
        RoundTrip(new CreateTerm("red", category));
        RoundTrip(new UpdateTerm(id, "crimson", category, true));
        RoundTrip(new DeleteTerm(id));
    }

    private static void RoundTrip<T>(T command) => IpcJson.FromElement<T>(IpcJson.ToElement(command).ShouldSucceed()).ShouldSucceed().ShouldBe(command);
}

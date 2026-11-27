namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal static class TestAccounts
{
    public static OneDriveAccounts Create() => For(new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}"))), new OneDriveTelemetry());

    public static OneDriveAccounts For(HttpClient client, OneDriveTelemetry telemetry)
        => new(holder => new AccountClients(holder, new RootEnumerator(client, telemetry), new ItemTransfer(client, client)));
}

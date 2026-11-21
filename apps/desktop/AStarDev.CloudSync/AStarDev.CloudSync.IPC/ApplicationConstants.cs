namespace AStarDev.CloudSync.IPC;

/// <summary>Wire-protocol strings. These are the contract with the peer, so they must not change casually.</summary>
internal static class ApplicationConstants
{
    public const string EnvelopeKindDiscriminator = "kind";
    public const string CommandKind = "command";
    public const string QueryKind = "query";
    public const string SubscribeKind = "subscribe";
    public const string ResponseKind = "response";
    public const string ErrorKind = "error";
    public const string EventKind = "event";
    public const string TelemetryName = "AStarDev.CloudSync.IPC";
    public const string ClientSide = "client";
    public const string ServerSide = "server";
    public const string SubscribeMessageType = "Subscribe";
}

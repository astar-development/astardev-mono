namespace AStarDev.CloudSync.IPC;

/// <summary>Published after a <see cref="ConnectAccount"/> succeeded. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="DisplayName">The account's name as the provider shows it, such as the sign-in address.</param>
public sealed record AccountConnected(string DisplayName) : IEvent;

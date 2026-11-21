namespace AStarDev.CloudSync.IPC;

/// <summary>Published after a <see cref="DisconnectAccount"/> succeeded. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account that is gone.</param>
public sealed record AccountDisconnected(Guid AccountId) : IEvent;

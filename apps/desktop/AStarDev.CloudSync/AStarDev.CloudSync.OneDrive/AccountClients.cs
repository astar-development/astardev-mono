namespace AStarDev.CloudSync.OneDrive;

/// <summary>The token holder and Graph clients of one signed-in account.</summary>
/// <param name="Holder">Holds the account's access token.</param>
/// <param name="Enumerator">Lists the account's drive.</param>
/// <param name="Transfer">Moves items in and out of the account's drive.</param>
internal sealed record AccountClients(AccessTokenHolder Holder, RootEnumerator Enumerator, ItemTransfer Transfer);

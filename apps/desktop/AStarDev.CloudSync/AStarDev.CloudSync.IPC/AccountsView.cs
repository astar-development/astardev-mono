namespace AStarDev.CloudSync.IPC;

/// <summary>The accounts the daemon holds, answering <see cref="GetAccounts"/>.</summary>
/// <param name="Accounts">Oldest connection first. Empty when no account is connected.</param>
public sealed record AccountsView(IReadOnlyList<AccountView> Accounts);

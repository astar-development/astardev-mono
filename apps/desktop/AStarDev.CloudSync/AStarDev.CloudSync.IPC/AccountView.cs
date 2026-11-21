namespace AStarDev.CloudSync.IPC;

/// <summary>One connected account as the UI shows it.</summary>
/// <param name="Id">Identifies the account in <see cref="DisconnectAccount"/>.</param>
/// <param name="DisplayName">The account's name as the provider shows it, such as the sign-in address.</param>
/// <param name="ProviderType">Which provider the account belongs to, such as <c>OneDrive</c>.</param>
/// <param name="State">Whether the account is usable or needs the user to sign in again.</param>
public sealed record AccountView(Guid Id, string DisplayName, string ProviderType, AccountState State);

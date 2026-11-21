using AStarDev.CloudSync.IPC;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One account on the accounts list.</summary>
/// <param name="account">What the daemon reported.</param>
/// <param name="owner">The list the card belongs to, which sends the requests.</param>
public sealed partial class AccountCardViewModel(AccountView account, AccountsViewModel owner) : ViewModelBase
{
    /// <summary>What the daemon reported about the account, for screens that show it their own way.</summary>
    public AccountView Account => account;

    /// <summary>The account's name as the provider shows it.</summary>
    public string DisplayName => account.DisplayName;

    /// <summary>One line saying whether the account works or needs the user to sign in again.</summary>
    public string StatusText => CanReconnect ? ApplicationConstants.ReauthenticationRequiredText : ApplicationConstants.AccountConnectedText;

    /// <summary>True when the provider refused the stored sign-in, so the card should offer Reconnect.</summary>
    public bool CanReconnect => account.State == AccountState.ReauthenticationRequired;

    /// <summary>Forgets the account and its stored sign-in. Also exposed to the view as <c>DisconnectCommand</c>.</summary>
    [RelayCommand]
    public Task DisconnectAsync(CancellationToken cancellationToken) => owner.DisconnectAsync(account, cancellationToken);

    /// <summary>Signs in again through the browser. Also exposed to the view as <c>ReconnectCommand</c>.</summary>
    [RelayCommand]
    public Task ReconnectAsync(CancellationToken cancellationToken) => owner.ReconnectAsync(cancellationToken);
}

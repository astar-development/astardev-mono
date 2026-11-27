using System.Collections.Concurrent;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Keeps one <see cref="AccountClients"/> per account, so a call for one account never carries another account's token. Register one instance for the process.</summary>
internal sealed class OneDriveAccounts(Func<AccessTokenHolder, AccountClients> create)
{
    private readonly ConcurrentDictionary<string, AccountClients> clients = new();

    /// <summary>The clients of an account. An account never signed in gets clients with no token, so Graph answers 401.</summary>
    public AccountClients For(string accountIdentifier) => clients.GetOrAdd(accountIdentifier, _ => create(new AccessTokenHolder()));

    /// <summary>Holds the token for the account's later requests, replacing its earlier one. Other accounts keep theirs.</summary>
    public void Set(string accountIdentifier, string token) => For(accountIdentifier).Holder.Set(token);

    /// <summary>Holds the token and the time it expires for the account's later requests, replacing its earlier one. Other accounts keep theirs.</summary>
    public void Set(string accountIdentifier, string token, DateTimeOffset expiresOn) => For(accountIdentifier).Holder.Set(token, expiresOn);

    /// <summary>Forgets the account's token. Safe to call for an account that never signed in.</summary>
    public void Clear(string accountIdentifier)
    {
        if (clients.TryGetValue(accountIdentifier, out var known)) known.Holder.Clear();
    }
}

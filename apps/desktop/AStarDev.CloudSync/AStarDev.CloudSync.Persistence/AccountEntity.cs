using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of an <see cref="Account"/>.</summary>
internal sealed class AccountEntity
{
    /// <summary>Primary key.</summary>
    public AccountId Id { get; set; }

    /// <summary>Provider name such as <c>OneDrive</c>. Unique together with <see cref="AccountIdentifier"/>.</summary>
    public required string ProviderType { get; set; }

    /// <summary>The name shown to the user.</summary>
    public required string DisplayName { get; set; }

    /// <summary>The provider's own id for the account.</summary>
    public required string AccountIdentifier { get; set; }

    /// <summary>When the account was first connected; stored as UTC and used to order account listings.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Whether the stored sign-in still works; stored as the enum name and <see cref="AccountStatus.Connected"/> for rows written before the column existed.</summary>
    public AccountStatus Status { get; set; }

    /// <summary>Copies the domain account into its stored form.</summary>
    /// <param name="account">The account to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static AccountEntity From(Account account)
        => new() { Id = account.Id, ProviderType = account.ProviderType, DisplayName = account.DisplayName, AccountIdentifier = account.AccountIdentifier, CreatedUtc = account.CreatedUtc, Status = account.Status };

    /// <summary>Builds the domain account from the stored row.</summary>
    /// <returns>An immutable account with the same values.</returns>
    public Account ToAccount() => new(Id, ProviderType, DisplayName, AccountIdentifier, CreatedUtc, Status);
}

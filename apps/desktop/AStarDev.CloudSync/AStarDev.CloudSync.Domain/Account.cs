namespace AStarDev.CloudSync.Domain;

/// <summary>A connected cloud account, such as a personal or business OneDrive.</summary>
/// <param name="Id">Our identity for the account, stable even if the provider changes how it names it.</param>
/// <param name="ProviderType">Which provider the account belongs to, such as <c>OneDrive</c>.</param>
/// <param name="DisplayName">The name shown to the user.</param>
/// <param name="AccountIdentifier">The provider's own id for the account. Unique per <paramref name="ProviderType"/>.</param>
/// <param name="CreatedUtc">When the account was first connected.</param>
public sealed record Account(AccountId Id, string ProviderType, string DisplayName, string AccountIdentifier, DateTimeOffset CreatedUtc);

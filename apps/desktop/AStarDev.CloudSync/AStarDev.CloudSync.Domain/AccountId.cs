namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for a connected cloud account. Not the id the provider uses; that is <c>ProviderAccount.AccountIdentifier</c>.</summary>
/// <param name="Value">Unique per connected account.</param>
public readonly record struct AccountId(Guid Value);

using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>No account has the requested identity. Retrying will not help unless the account is added first.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record AccountNotFound(AccountId Id);

/// <summary>An account with the same identity, or the same provider and provider account id, is already stored. Nothing was written.</summary>
/// <param name="Id">The identity of the account that was being added.</param>
public sealed record AccountAlreadyExists(AccountId Id);

/// <summary>The store could not complete the request, for example because the database is locked, unreadable or in recovery mode.</summary>
/// <param name="Message">The underlying reason, suitable for logs.</param>
public sealed record StorageFailed(string Message);

/// <summary>Closed set of failures that cross the state store boundary.</summary>
public union StoreError(AccountNotFound, AccountAlreadyExists, StorageFailed);

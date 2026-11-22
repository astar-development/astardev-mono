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

/// <summary>No sync job has the requested identity. Retrying will not help unless the job is added first.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record SyncJobNotFound(SyncJobId Id);

/// <summary>A sync job is already stored and the MVP allows a single job, so nothing was written.</summary>
/// <param name="ExistingId">The identity of the job that is already stored.</param>
public sealed record SyncJobAlreadyExists(SyncJobId ExistingId);

/// <summary>The job already holds this folder as a scope. Nothing was written.</summary>
/// <param name="Scope">The scope that was being added.</param>
public sealed record SyncScopeAlreadyExists(SyncScope Scope);

/// <summary>The job does not hold this folder as a scope.</summary>
/// <param name="Scope">The scope that was being removed.</param>
public sealed record SyncScopeNotFound(SyncScope Scope);

/// <summary>No running operation has the identity: it is missing, was never claimed, or has already finished. Nothing was changed.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record OperationNotRunning(OperationId Id);

/// <summary>No dead-lettered operation has the identity: it is missing, or is in another status, for example because it was already requeued or dropped. Nothing was changed.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record OperationNotDeadLettered(OperationId Id);

/// <summary>Closed set of failures that cross the state store boundary.</summary>
public union StoreError(AccountNotFound, AccountAlreadyExists, StorageFailed, SyncJobNotFound, SyncJobAlreadyExists, SyncScopeAlreadyExists, SyncScopeNotFound, OperationNotRunning, OperationNotDeadLettered);

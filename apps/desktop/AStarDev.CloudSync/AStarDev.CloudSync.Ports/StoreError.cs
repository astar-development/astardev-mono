using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports;

/// <summary>No account has the requested identity. Retrying will not help unless the account is added first.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record AccountNotFound(AccountId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.AccountNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>An account with the same identity, or the same provider and provider account id, is already stored. Nothing was written.</summary>
/// <param name="Id">The identity of the account that was being added.</param>
public sealed record AccountAlreadyExists(AccountId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.AccountAlreadyExists;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The store could not complete the request, for example because the database is locked, unreadable or in recovery mode.</summary>
/// <param name="Message">The underlying reason, suitable for logs.</param>
public sealed record StorageFailed(string Message) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.StorageFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>No sync job has the requested identity. Retrying will not help unless the job is added first.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record SyncJobNotFound(SyncJobId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.SyncJobNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The account already has a sync job and each account has one, so nothing was written.</summary>
/// <param name="ExistingId">The identity of the account's stored job.</param>
public sealed record SyncJobAlreadyExists(SyncJobId ExistingId) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.SyncJobAlreadyExists;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The job already holds this folder as a scope. Nothing was written.</summary>
/// <param name="Scope">The scope that was being added.</param>
public sealed record SyncScopeAlreadyExists(SyncScope Scope) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.SyncScopeAlreadyExists;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The job does not hold this folder as a scope.</summary>
/// <param name="Scope">The scope that was being removed.</param>
public sealed record SyncScopeNotFound(SyncScope Scope) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.SyncScopeNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>No running operation has the identity: it is missing, was never claimed, or has already finished. Nothing was changed.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record OperationNotRunning(OperationId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.OperationNotRunning;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>No dead-lettered operation has the identity: it is missing, or is in another status, for example because it was already requeued or dropped. Nothing was changed.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record OperationNotDeadLettered(OperationId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.OperationNotDeadLettered;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>No conflict has the identity. Retrying will not help; the conflict was never stored or went with its job. Nothing was changed.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record ConflictNotFound(ConflictId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.ConflictNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The name is not one of <see cref="SettingNames"/>. Nothing was written.</summary>
/// <param name="Name">The name that was given.</param>
public sealed record UnknownSetting(string Name) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.UnknownSetting;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The value does not fit the setting, for example text where a number of days is expected. Nothing was written.</summary>
/// <param name="Name">The setting that was being saved.</param>
/// <param name="Value">The value that was refused.</param>
public sealed record InvalidSettingValue(string Name, string Value) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.InvalidSettingValue;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Closed set of failures that cross the state store boundary.</summary>
public union StoreError(AccountNotFound, AccountAlreadyExists, StorageFailed, SyncJobNotFound, SyncJobAlreadyExists, SyncScopeAlreadyExists, SyncScopeNotFound, OperationNotRunning, OperationNotDeadLettered, ConflictNotFound, UnknownSetting, InvalidSettingValue);

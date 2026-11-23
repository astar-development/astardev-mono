using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports;

/// <summary>The provider asked us to slow down. Retry the same call, but not before <paramref name="RetryAfter"/> has passed.</summary>
/// <param name="RetryAfter">How long the provider asked us to wait.</param>
public sealed record RateLimited(TimeSpan RetryAfter) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.RateLimited;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>A temporary problem such as a network drop or a server error. Safe to retry, with back-off.</summary>
/// <param name="Message">Provider-supplied reason, suitable for logs.</param>
public sealed record Transient(string Message) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.Transient;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The provider refused our credentials. Retrying the same call will not help: the user must sign in again.</summary>
/// <param name="Message">Provider-supplied reason, suitable for logs.</param>
public sealed record AuthenticationFailed(string Message) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.AuthenticationFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The provider rejected the request for a reason that will not go away, such as a missing item or a forbidden operation. Do not retry.</summary>
/// <param name="Message">Provider-supplied reason, suitable for logs.</param>
public sealed record Permanent(string Message) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.Permanent;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The delta token is no longer accepted, for example because it expired. Retrying with it will not help: list the root again and start from the new token.</summary>
/// <param name="Message">Provider-supplied reason, suitable for logs.</param>
public sealed record InvalidDeltaToken(string Message) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.InvalidDeltaToken;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Closed set of failures that cross the provider boundary. Provider-specific errors are translated into these.</summary>
public union ProviderError(RateLimited, Transient, AuthenticationFailed, Permanent, InvalidDeltaToken);

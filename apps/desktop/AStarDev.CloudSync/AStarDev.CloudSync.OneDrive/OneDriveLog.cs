using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.OneDrive;

internal static partial class OneDriveLog
{
    internal static void CallFailed(ILogger logger, string call, ProviderError error)
    {
        switch (error)
        {
            case RateLimited limited:
                RateLimitEncountered(logger, call, limited.RetryAfter.TotalSeconds, limited.Code);
                break;
            case AuthenticationFailed authentication:
                AuthenticationExpired(logger, call, authentication.Code);
                break;
            case InvalidDeltaToken invalid:
                DeltaTokenInvalid(logger, call, invalid.Code);
                break;
            case Transient transient:
                TransientFailure(logger, call, transient.Message, transient.Code);
                break;
            case Permanent permanent:
                PermanentFailure(logger, call, permanent.Message, permanent.Code);
                break;
        }
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning, Message = "Rate limit encountered on {Call}; Graph asked for a wait of {RetryAfterSeconds} seconds ({ErrorCode})")]
    private static partial void RateLimitEncountered(ILogger logger, string call, double retryAfterSeconds, ErrorCode errorCode);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Authentication expired on {Call}; the account must sign in again ({ErrorCode})")]
    private static partial void AuthenticationExpired(ILogger logger, string call, ErrorCode errorCode);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Delta token invalid on {Call} ({ErrorCode})")]
    private static partial void DeltaTokenInvalid(ILogger logger, string call, ErrorCode errorCode);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "{Call} failed temporarily: {Reason} ({ErrorCode})")]
    private static partial void TransientFailure(ILogger logger, string call, string reason, ErrorCode errorCode);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Error, Message = "{Call} failed: {Reason} ({ErrorCode})")]
    private static partial void PermanentFailure(ILogger logger, string call, string reason, ErrorCode errorCode);
}

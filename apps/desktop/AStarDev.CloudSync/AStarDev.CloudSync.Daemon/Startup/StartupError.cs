using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Why a startup stage failed. <paramref name="Exception"/> is set when the cause was a captured exception.</summary>
internal sealed record StartupError(string StageName, string Cause, Exception? Exception = null) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => DaemonErrorCodes.StartupStageFailed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

namespace AStarDev.CloudSync.Errors;

/// <summary>What every error the app can raise tells its reader: a stable code and where to read about it.</summary>
public interface IErrorInfo
{
    /// <summary>The stable code of this kind of error. Two kinds never share a code.</summary>
    ErrorCode Code { get; }

    /// <summary>The page for <see cref="Code"/> under <see cref="ErrorLinks.Root"/>. The page may not exist yet.</summary>
    string HelpLink { get; }
}

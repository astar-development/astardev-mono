using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Classification;

/// <summary>The shipped seed could not be read or does not make sense, for example a term names a category the seed does not define. Retrying will not help; the app build is wrong.</summary>
/// <param name="Reason">What is wrong with the seed, suitable for logs. Never holds a file path or term.</param>
internal sealed record InvalidSeed(string Reason) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => ClassificationErrorCodes.InvalidSeed;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

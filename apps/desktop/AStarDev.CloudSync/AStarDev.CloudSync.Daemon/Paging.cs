namespace AStarDev.CloudSync.Daemon;

/// <summary>Brings the page a UI asks for into the range one IPC answer may carry.</summary>
internal static class Paging
{
    /// <summary>The number of items to pass over: the requested number, a negative one counting as zero.</summary>
    /// <param name="skip">What the query asked for.</param>
    /// <returns>Zero or more.</returns>
    public static int Skip(int skip) => Math.Max(skip, 0);

    /// <summary>The most items to return: the requested number brought into 1 to <see cref="ApplicationConstants.ListLimit"/>.</summary>
    /// <param name="take">What the query asked for.</param>
    /// <returns>Between 1 and <see cref="ApplicationConstants.ListLimit"/>.</returns>
    public static int Take(int take) => Math.Clamp(take, 1, ApplicationConstants.ListLimit);
}

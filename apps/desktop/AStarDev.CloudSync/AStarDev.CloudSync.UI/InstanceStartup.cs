using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;

namespace AStarDev.CloudSync.UI;

/// <summary>Decides what the UI does with the outcome of the single-instance claim.</summary>
internal static class InstanceStartup
{
    private const int Success = 0;
    private const int Failure = 1;

    /// <summary>The process exit code when the UI must not start, or none when it is the running instance and should keep starting.</summary>
    /// <param name="claim">What happened when the UI tried to become the running instance.</param>
    /// <returns>Exit code 0 when another instance took over (<see cref="InstanceClaim.HandedOff"/>, <see cref="InstanceClaim.NotAnswered"/>), 1 when single instance cannot be enforced (<see cref="InstanceClaim.LockUnavailable"/>), none for <see cref="InstanceClaim.Primary"/>.</returns>
    public static Option<int> ExitCodeFor(InstanceClaim claim)
        => claim switch
        {
            InstanceClaim.HandedOff or InstanceClaim.NotAnswered => Option.Some(Success),
            InstanceClaim.LockUnavailable => Option.Some(Failure),
            _ => Option.None<int>()
        };
}

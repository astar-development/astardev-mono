namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>Maps operation statuses to the trust levels that give them colour and icon.</summary>
internal static class OperationStatusLevel
{
    /// <summary>The trust level showing <paramref name="status"/>: pending is degraded, running active, completed healthy, failed attention.</summary>
    public static TrustLevel Of(OperationStatus status) => status switch
    {
        OperationStatus.Pending => TrustLevel.Degraded,
        OperationStatus.Running => TrustLevel.Active,
        OperationStatus.Completed => TrustLevel.Healthy,
        _ => TrustLevel.Attention
    };
}

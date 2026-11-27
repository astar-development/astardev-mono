namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Where one job's operation runs: the local folder and the provider account whose drive it syncs.</summary>
/// <param name="Root">The job's local root folder.</param>
/// <param name="AccountIdentifier">The provider's id for the job's account.</param>
internal sealed record JobTarget(string Root, string AccountIdentifier);

namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetQueue"/>, also carried by <see cref="QueueChanged"/>. The daemon counts; the UI only shows the numbers.</summary>
/// <param name="Pending">Operations waiting to start.</param>
/// <param name="Running">Operations executing now.</param>
/// <param name="Completed">Operations finished successfully.</param>
/// <param name="Failed">Operations whose last attempt failed, those that gave up after their retries included.</param>
/// <param name="Operations">The newest pending and running operations, newest first; empty when nothing waits or runs. Holds at most 500 entries, so <paramref name="Pending"/> and <paramref name="Running"/> can count more than are listed.</param>
public sealed record QueueView(int Pending, int Running, int Completed, int Failed, IReadOnlyList<QueuedOperationView> Operations);

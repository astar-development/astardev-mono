namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Published while a reclassify run goes on, after each batch of files, and once more when it ends. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.
/// The daemon counts; the UI only shows the numbers.
/// </summary>
/// <param name="Running"><see langword="false"/> on the last report of a run, after which no more files are expected from it.</param>
/// <param name="FilesDone">Files gone through so far in the run.</param>
public sealed record ReclassifyProgressChanged(bool Running, long FilesDone) : IEvent;

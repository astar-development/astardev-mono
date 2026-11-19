namespace AStarDev.CloudSync.IPC;

/// <summary>The server handled the request and reported a failure.</summary>
public sealed class IpcException(string message) : Exception(message);

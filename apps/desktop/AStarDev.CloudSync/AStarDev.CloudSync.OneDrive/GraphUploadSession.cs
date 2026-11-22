namespace AStarDev.CloudSync.OneDrive;

/// <summary>What Graph returns when an upload session opens.</summary>
/// <param name="UploadUrl">Pre-authenticated URL that receives the content chunks.</param>
internal sealed record GraphUploadSession(string UploadUrl);

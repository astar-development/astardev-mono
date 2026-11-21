using System.Text.Json;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>The serializer settings for Graph payloads.</summary>
internal static class GraphJson
{
    /// <summary>Case-insensitive property matching, as Graph casing is not guaranteed across endpoints.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

using System.Text.Json.Serialization;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>One page of a Graph children listing. Stays inside the provider so no Graph shape crosses the boundary.</summary>
/// <param name="Value">The items on this page. <c>null</c> when Graph sent none.</param>
/// <param name="NextLink">Absolute URL of the next page, or <c>null</c> on the last page.</param>
internal sealed record GraphChildrenPage(IReadOnlyList<GraphDriveItem>? Value, [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

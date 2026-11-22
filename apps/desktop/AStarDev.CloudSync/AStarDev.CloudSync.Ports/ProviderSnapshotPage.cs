using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>One page of remote items, with the means to continue after it.</summary>
/// <param name="Items">The items on this page. Empty when the root holds nothing.</param>
/// <param name="ResumeToken">Opaque provider text that continues the listing after this page; pass it back unchanged to <see cref="ICloudProvider.GetSnapshotAsync"/>. <see langword="null"/> on the last page.</param>
/// <param name="DeltaToken">The bookmark for "changes since this listing". Only set on the last page, together with a <see langword="null"/> <paramref name="ResumeToken"/>; <see langword="null"/> when the provider has none to give.</param>
/// <param name="TotalItems">How many items the whole listing holds, when the provider knows up front. <see langword="null"/> when it does not, in which case no percentage or time estimate can be given.</param>
public sealed record ProviderSnapshotPage(IReadOnlyList<ProviderItem> Items, string? ResumeToken = null, DeltaToken? DeltaToken = null, long? TotalItems = null);

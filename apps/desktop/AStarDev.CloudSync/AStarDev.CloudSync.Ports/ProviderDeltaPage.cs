using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>One page of changes, with the means to continue after it.</summary>
/// <param name="Changes">The changes on this page in the order the provider reports them. Empty when nothing changed.</param>
/// <param name="ResumeToken">Opaque provider text that continues after this page; pass it back unchanged to <see cref="ICloudProvider.GetDeltasAsync"/>. <see langword="null"/> on the last page.</param>
/// <param name="DeltaToken">The new bookmark for "changes since now". Only set on the last page, together with a <see langword="null"/> <paramref name="ResumeToken"/>. The old token is spent once this is stored.</param>
public sealed record ProviderDeltaPage(IReadOnlyList<ProviderChange> Changes, string? ResumeToken = null, DeltaToken? DeltaToken = null);

namespace AStarDev.CloudSync.Ports;

/// <summary>One page of a list too long to read at once.</summary>
/// <typeparam name="T">What the list holds.</typeparam>
/// <param name="Total">How many items the whole list holds, so a caller can ask for the next page. At least <c>Items.Count</c>.</param>
/// <param name="Items">The items of this page, in the order the method that returned it states; empty past the end of the list.</param>
public sealed record Page<T>(int Total, IReadOnlyList<T> Items);

namespace AStarDev.CloudSync.Errors;

/// <summary>Why <see cref="ErrorCode.Create"/> refused a value: it does not read <c>CS-&lt;MODULE&gt;-&lt;NNNN&gt;</c>.</summary>
/// <param name="Value">The rejected text, exactly as given. Empty when the caller passed nothing.</param>
public sealed record InvalidErrorCode(string Value);

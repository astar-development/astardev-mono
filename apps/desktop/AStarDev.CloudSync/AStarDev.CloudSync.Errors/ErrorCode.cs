using System.Text.RegularExpressions;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Errors;

/// <summary>A stable code for one kind of error, written <c>CS-&lt;MODULE&gt;-&lt;NNNN&gt;</c>, for example <c>CS-IPC-0003</c>.</summary>
/// <remarks>Codes never change once shipped: the help link and every log line quote them.</remarks>
public readonly partial record struct ErrorCode
{
    private readonly string? text;

    private ErrorCode(string value) => text = value;

    /// <summary>The code text. Never empty for a value made by <see cref="Create"/>; the empty string, never <see langword="null"/>, for <c>default</c>.</summary>
    public string Value => text ?? string.Empty;

    /// <summary>Checks <paramref name="value"/> against the <c>CS-&lt;MODULE&gt;-&lt;NNNN&gt;</c> format: a module of 2 to 12 capital letters, then 4 digits.</summary>
    /// <param name="value">The text to check. May be <see langword="null"/> or empty, which is a failure.</param>
    /// <returns>The code, or <see cref="InvalidErrorCode"/> when the text does not match the format.</returns>
    public static Result<ErrorCode, InvalidErrorCode> Create(string? value) =>
        value is not null && Format().IsMatch(value)
            ? new Ok<ErrorCode>(new ErrorCode(value))
            : new Fail<InvalidErrorCode>(new InvalidErrorCode(value ?? string.Empty));

    /// <summary>The code text.</summary>
    public override string ToString() => Value;

    [GeneratedRegex(@"^CS-[A-Z]{2,12}-[0-9]{4}\z")]
    private static partial Regex Format();
}

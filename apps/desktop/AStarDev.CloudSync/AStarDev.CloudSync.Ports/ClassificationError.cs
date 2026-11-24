using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports;

/// <summary>No category has the requested identity. Retrying will not help unless the category is added first.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record CategoryNotFound(CategoryId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.CategoryNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>A category with the same identity or the same name, ignoring case, is already stored. Nothing was written.</summary>
/// <param name="Name">The name that was being added or given.</param>
public sealed record CategoryAlreadyExists(string Name) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.CategoryAlreadyExists;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The category still holds terms, so it cannot be removed. Nothing was changed.</summary>
/// <param name="Id">The category that was being removed.</param>
public sealed record CategoryNotEmpty(CategoryId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.CategoryNotEmpty;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>No term has the requested identity. Retrying will not help unless the term is added first.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record TermNotFound(TermId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.TermNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The category already holds a term with the same text, or the identity is taken. Nothing was written.</summary>
/// <param name="Text">The term text that was being added or given.</param>
/// <param name="CategoryId">The category the text was going into.</param>
public sealed record TermAlreadyExists(string Text, CategoryId CategoryId) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.TermAlreadyExists;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>No synchronised item has the requested identity, so it cannot be linked to a term. Nothing was written.</summary>
/// <param name="Id">The identity that was looked up.</param>
public sealed record ItemNotFound(ItemId Id) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.ItemNotFound;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>The text is empty once trimmed, so it cannot be a term. Nothing was written.</summary>
/// <param name="Text">The text that was given.</param>
public sealed record InvalidTermText(string Text) : IErrorInfo
{
    /// <inheritdoc/>
    public ErrorCode Code => PortsErrorCodes.InvalidTermText;

    /// <inheritdoc/>
    public string HelpLink => ErrorLinks.For(Code);
}

/// <summary>Closed set of failures that cross the classification store boundary.</summary>
public union ClassificationError(CategoryNotFound, CategoryAlreadyExists, CategoryNotEmpty, TermNotFound, TermAlreadyExists, ItemNotFound, InvalidTermText, StorageFailed);

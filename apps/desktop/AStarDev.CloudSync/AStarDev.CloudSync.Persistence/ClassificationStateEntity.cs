namespace AStarDev.CloudSync.Persistence;

/// <summary>One named fact classification remembers between runs, such as the applied seed version.</summary>
internal sealed class ClassificationStateEntity
{
    /// <summary>The primary key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The stored value.</summary>
    public string Value { get; set; } = string.Empty;
}

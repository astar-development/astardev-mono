namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>A resource file is not valid JSON of string values.</summary>
/// <param name="Feature">The feature the file belongs to.</param>
/// <param name="Reason">The parser's explanation.</param>
internal sealed record MalformedResource(string Feature, string Reason);

/// <summary>Two resource files define the same key.</summary>
/// <param name="Key">The key defined twice.</param>
internal sealed record DuplicateKey(string Key);

/// <summary>A key does not start with the name of the feature whose file defines it, so it could collide with another feature.</summary>
/// <param name="Feature">The feature whose file holds the key.</param>
/// <param name="Key">The offending key.</param>
internal sealed record ForeignKey(string Feature, string Key);

/// <summary>Everything that can go wrong merging the resource files, as values. Match it exhaustively.</summary>
internal union LocalisationError(MalformedResource, DuplicateKey, ForeignKey);

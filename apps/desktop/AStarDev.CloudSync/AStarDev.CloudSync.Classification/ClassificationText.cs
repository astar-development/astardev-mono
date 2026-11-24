using System.Text;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Shared text rules for term text.</summary>
internal static class ClassificationText
{
    /// <summary>Trims, composes to NFC, lower-cases and collapses runs of whitespace to one space.</summary>
    /// <param name="text">The text to normalise.</param>
    /// <returns>The normalised text, or none when nothing is left. Text that cannot be composed (an unpaired surrogate) is kept as it is.</returns>
    internal static Option<string> NormaliseTerm(string text)
    {
        var composed = Try.Run(() => text.Normalize(NormalizationForm.FormC)).Match(value => value, _ => text);
        var collapsed = string.Join(' ', composed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        return collapsed.Length == 0 ? Option.None<string>() : Option.Some(collapsed);
    }
}

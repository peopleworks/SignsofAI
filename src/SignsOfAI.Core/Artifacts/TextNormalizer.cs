using System.Text;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Artifacts;

/// <summary>
/// A cleaned copy of the text together with the map back to where every character came from.
/// The map is what lets findings computed on the clean copy be highlighted in the original.
/// </summary>
public sealed class NormalizedText
{
    /// <summary>The cleaned text: artifacts removed, impostor letters replaced by the real ones.</summary>
    public string Text { get; }

    /// <summary>
    /// For each character of <see cref="Text"/>, its offset in the original — plus one final entry
    /// holding the original length, so the end of a span maps as cleanly as its start.
    /// </summary>
    private readonly int[] _sourceOffsets;

    public bool Changed { get; }

    private NormalizedText(string text, int[] sourceOffsets, bool changed)
    {
        Text = text;
        _sourceOffsets = sourceOffsets;
        Changed = changed;
    }

    public static NormalizedText Unchanged(string text) => new(text, [], false);

    internal static NormalizedText Cleaned(string text, int[] offsets) => new(text, offsets, true);

    /// <summary>Where a position in the cleaned text sits in the original.</summary>
    public int ToSource(int index)
    {
        if (!Changed) return index;
        if (index < 0) return 0;
        return index < _sourceOffsets.Length ? _sourceOffsets[index] : _sourceOffsets[^1];
    }

    /// <summary>
    /// The same span, expressed against the original text. Start and end are mapped separately
    /// because cleaning changes lengths — a removed zero-width character inside a word makes the
    /// original span longer than the cleaned one.
    /// </summary>
    public TextSpan ToSource(TextSpan span)
    {
        if (!Changed) return span;
        int start = ToSource(span.Start);
        int end = ToSource(span.Start + span.Length);
        return new TextSpan(start, Math.Max(0, end - start));
    }
}

/// <summary>
/// Produces the text the analyzers actually run against.
///
/// This exists because of an attack the rest of the engine cannot see. Every lexical and pattern rule
/// matches on words, so replacing one letter of "delve" with the Cyrillic letter that looks identical
/// makes the rule miss while the page looks unchanged — the published research does exactly this and
/// drives seven detectors below chance. Normalizing first is what keeps the catalog honest, and it
/// matters twice over for per-author comparison, where the same trick can be used to poison the
/// baseline a writer is measured against.
///
/// It consumes an <see cref="ArtifactReport"/> rather than re-deciding what an artifact is, so the
/// cleaning and the report can never disagree about what was found. And nothing is cleaned silently:
/// whatever is removed here is reported there.
/// </summary>
public static class TextNormalizer
{
    /// <summary>Scans and cleans in one step.</summary>
    public static NormalizedText Normalize(string? text) =>
        Apply(text, ArtifactScanner.Scan(text));

    /// <summary>Cleans <paramref name="text"/> according to an already-computed report.</summary>
    public static NormalizedText Apply(string? text, ArtifactReport report)
    {
        text ??= string.Empty;
        if (!report.Any)
            return NormalizedText.Unchanged(text);

        var byPosition = new Dictionary<int, ArtifactOccurrence>(report.Count);
        foreach (var occurrence in report.Occurrences)
            byPosition.TryAdd(occurrence.Span.Start, occurrence);

        var builder = new StringBuilder(text.Length);
        var offsets = new List<int>(text.Length + 1);

        int i = 0;
        while (i < text.Length)
        {
            if (byPosition.TryGetValue(i, out var artifact))
            {
                switch (artifact.Kind)
                {
                    // An impostor letter becomes the letter it was pretending to be, so the rules see
                    // the word the reader sees.
                    case ArtifactKind.LookalikeLetter when artifact.LooksLike is { Length: > 0 } letter:
                        builder.Append(letter);
                        offsets.Add(i);
                        break;

                    // An unusual space becomes an ordinary one: it is still a word boundary, and
                    // deleting it would join two words into a nonsense token.
                    case ArtifactKind.UnusualSpace:
                        builder.Append(' ');
                        offsets.Add(i);
                        break;

                    // Everything else contributes nothing to the reading and is dropped.
                    default:
                        break;
                }

                i += Math.Max(1, artifact.Span.Length);
                continue;
            }

            builder.Append(text[i]);
            offsets.Add(i);
            i++;
        }

        offsets.Add(text.Length);
        return NormalizedText.Cleaned(builder.ToString(), [.. offsets]);
    }
}

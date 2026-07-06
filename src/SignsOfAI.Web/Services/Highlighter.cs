using SignsOfAI.Core.Model;

namespace SignsOfAI.Web.Services;

/// <summary>A run of source text, optionally tied to the finding that highlights it.</summary>
public readonly record struct HighlightSegment(string Text, Finding? Finding);

/// <summary>
/// Splits source text into segments for rendering: plain runs interleaved with highlighted
/// runs. Overlapping findings are resolved by preferring the stronger (higher-weight) one;
/// zero-length (document-level) findings such as burstiness are not highlighted inline.
/// </summary>
public static class Highlighter
{
    public static IReadOnlyList<HighlightSegment> Build(string source, IReadOnlyList<Finding> findings)
    {
        var segments = new List<HighlightSegment>();
        if (string.IsNullOrEmpty(source))
            return segments;

        var inline = findings
            .Where(f => f.Span.Length > 0 && f.Span.End <= source.Length)
            .OrderBy(f => f.Span.Start)
            .ThenByDescending(f => f.Weight)
            .ThenByDescending(f => f.Span.Length)
            .ToList();

        int cursor = 0;
        foreach (var f in inline)
        {
            if (f.Span.Start < cursor) // overlaps an already-emitted highlight → skip
                continue;

            if (f.Span.Start > cursor)
                segments.Add(new HighlightSegment(source[cursor..f.Span.Start], null));

            segments.Add(new HighlightSegment(source.Substring(f.Span.Start, f.Span.Length), f));
            cursor = f.Span.End;
        }

        if (cursor < source.Length)
            segments.Add(new HighlightSegment(source[cursor..], null));

        return segments;
    }

    /// <summary>CSS class suffix per category, used for coloring highlights and badges.</summary>
    public static string CategoryClass(SignCategory category) => category switch
    {
        SignCategory.Lexical => "lex",
        SignCategory.Rhetorical => "rhet",
        SignCategory.Syntactic => "syn",
        SignCategory.Statistical => "stat",
        _ => "lex",
    };

    /// <summary>Hex color per category — used where CSS vars aren't available (e.g. canvas).</summary>
    public static string CategoryHex(SignCategory category) => category switch
    {
        SignCategory.Lexical => "#db2777",
        SignCategory.Rhetorical => "#d97706",
        SignCategory.Syntactic => "#0891b2",
        SignCategory.Statistical => "#dc2626",
        _ => "#db2777",
    };

    public static string CategoryLabel(SignCategory category) => category switch
    {
        SignCategory.Lexical => "Lexical",
        SignCategory.Rhetorical => "Rhetorical",
        SignCategory.Syntactic => "Syntactic",
        SignCategory.Statistical => "Statistical",
        _ => category.ToString(),
    };
}

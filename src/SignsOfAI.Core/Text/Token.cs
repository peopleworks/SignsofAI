using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Text;

/// <summary>A word token with its position in the source text.</summary>
public readonly record struct WordToken(string Text, TextSpan Span)
{
    /// <summary>Lower-cased form for case-insensitive matching.</summary>
    public string Normalized => Text.ToLowerInvariant();
}

/// <summary>A sentence with its word count and position, used for burstiness.</summary>
public readonly record struct Sentence(TextSpan Span, int WordCount);

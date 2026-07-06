using System.Text.RegularExpressions;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Text;

/// <summary>
/// A tokenized view of the input text: raw string plus words, sentences and paragraphs
/// with character offsets so findings can be highlighted precisely.
/// </summary>
public sealed partial class TextDocument
{
    public string Raw { get; }
    public IReadOnlyList<WordToken> Words { get; }
    public IReadOnlyList<Sentence> Sentences { get; }
    public int ParagraphCount { get; }

    public TextDocument(string raw)
    {
        Raw = raw ?? string.Empty;
        Words = TokenizeWords(Raw);
        Sentences = SplitSentences(Raw);
        ParagraphCount = CountParagraphs(Raw);
    }

    // A word: a run of letters/marks, allowing internal apostrophes/hyphens (e.g. "it's", "state-of-the-art").
    [GeneratedRegex(@"\p{L}[\p{L}\p{M}'’\-]*", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    // Sentence terminators, keeping the space so offsets stay aligned.
    [GeneratedRegex(@"[.!?…]+[)""'”’\]]*(\s+|$)", RegexOptions.Compiled)]
    private static partial Regex SentenceBoundaryRegex();

    private static List<WordToken> TokenizeWords(string text)
    {
        var words = new List<WordToken>();
        foreach (Match m in WordRegex().Matches(text))
            words.Add(new WordToken(m.Value, new TextSpan(m.Index, m.Length)));
        return words;
    }

    private static List<Sentence> SplitSentences(string text)
    {
        var sentences = new List<Sentence>();
        if (string.IsNullOrWhiteSpace(text))
            return sentences;

        int start = 0;
        foreach (Match m in SentenceBoundaryRegex().Matches(text))
        {
            int end = m.Index + m.Length;
            AddSentence(sentences, text, start, end);
            start = end;
        }

        // Trailing sentence without terminal punctuation.
        if (start < text.Length)
            AddSentence(sentences, text, start, text.Length);

        return sentences;
    }

    private static void AddSentence(List<Sentence> sentences, string text, int start, int end)
    {
        // Trim leading whitespace from the span so highlights look clean.
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        if (start >= end) return;

        var span = new TextSpan(start, end - start);
        int wordCount = WordRegex().Matches(span.Slice(text)).Count;
        if (wordCount > 0)
            sentences.Add(new Sentence(span, wordCount));
    }

    private static int CountParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var blocks = ParagraphSplitRegex().Split(text);
        return blocks.Count(b => !string.IsNullOrWhiteSpace(b));
    }

    [GeneratedRegex(@"(\r?\n){2,}", RegexOptions.Compiled)]
    private static partial Regex ParagraphSplitRegex();
}

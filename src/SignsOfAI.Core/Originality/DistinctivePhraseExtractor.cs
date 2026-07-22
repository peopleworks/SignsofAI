using SignsOfAI.Core.Model;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Core.Originality;

/// <summary>A short, distinctive phrase worth searching for on the web, anchored back to the source text.</summary>
public sealed record DistinctivePhrase(string Phrase, TextSpan Span);

/// <summary>
/// Picks the handful of most <b>distinctive</b> phrases from a document — the ones most worth pasting into a
/// web search to see if they were copied from the internet. This is the honest core of the "web spot-check":
/// we can't index the whole web, so instead of pretending to, we surface the passages a human should check
/// (long, specific, proper-noun- or number-bearing wording — not generic filler) and hand them ready to search.
/// Deterministic and dependency-free; the actual searching happens in the browser, user-initiated.
/// </summary>
public static class DistinctivePhraseExtractor
{
    /// <summary>
    /// Returns up to <paramref name="maxPhrases"/> distinctive phrases, each at most <paramref name="maxWords"/>
    /// words (a length that exact-phrase web search handles well), most distinctive first.
    /// </summary>
    public static IReadOnlyList<DistinctivePhrase> Extract(string text, int maxPhrases = 5, int maxWords = 10)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var doc = new TextDocument(text);
        var words = doc.Words;
        var scored = new List<(DistinctivePhrase phrase, int score)>();

        foreach (var s in doc.Sentences)
        {
            // Words that fall inside this sentence, in order.
            var sw = new List<WordToken>();
            foreach (var w in words)
                if (w.Span.Start >= s.Span.Start && w.Span.End <= s.Span.End) sw.Add(w);
            if (sw.Count < 4) continue; // too short to be a distinctive fingerprint

            int win = Math.Min(maxWords, sw.Count);
            int bestStart = 0, bestScore = -1;
            for (int i = 0; i + win <= sw.Count; i++)
            {
                int sc = 0;
                for (int j = i; j < i + win; j++)
                    if (IsDistinctive(sw[j].Text, isSentenceStart: j == 0)) sc++;
                if (sc > bestScore) { bestScore = sc; bestStart = i; }
            }
            if (bestScore <= 0) continue; // nothing specific — skip generic sentences

            int startChar = sw[bestStart].Span.Start;
            int endChar = sw[bestStart + win - 1].Span.End;
            var phrase = text.Substring(startChar, endChar - startChar).Trim();
            if (phrase.Length == 0) continue;
            scored.Add((new DistinctivePhrase(phrase, new TextSpan(startChar, endChar - startChar)), bestScore));
        }

        return scored
            .OrderByDescending(r => r.score)
            .Take(maxPhrases)
            .Select(r => r.phrase)
            .ToList();
    }

    // A word carries "search signal" if it's long, has a digit (dates/figures), or is a mid-sentence
    // capitalized token (likely a proper noun). The first word of a sentence is capitalized by default,
    // so it doesn't count on its own.
    private static bool IsDistinctive(string word, bool isSentenceStart)
    {
        if (word.Length >= 7) return true;
        foreach (var c in word) if (char.IsDigit(c)) return true;
        if (!isSentenceStart && word.Length > 0 && char.IsUpper(word[0])) return true;
        return false;
    }
}

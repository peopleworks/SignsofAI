using System.Text.RegularExpressions;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Web.Services;

/// <summary>
/// A compact in-browser BM25 ranker over the rule catalog — the client-side lexical-search
/// pattern from the Banner/Windmill projects, ported to C#. Builds a tiny inverted index once,
/// then ranks by BM25 with light suffix stemming so a query returns the most relevant tells.
/// </summary>
public sealed partial class CatalogSearch
{
    private const double K1 = 1.5;
    private const double B = 0.6;

    private readonly IReadOnlyList<SignInfo> _docs;
    private readonly List<Dictionary<string, int>> _termFreqs = [];
    private readonly Dictionary<string, double> _idf = new();
    private readonly double _avgLen;

    public CatalogSearch(IReadOnlyList<SignInfo> docs)
    {
        _docs = docs;
        var docFreq = new Dictionary<string, int>();
        double totalLen = 0;

        foreach (var doc in docs)
        {
            var tf = new Dictionary<string, int>();
            var tokens = Tokenize(doc.SearchText);
            totalLen += tokens.Count;
            foreach (var t in tokens)
                tf[t] = tf.GetValueOrDefault(t) + 1;
            _termFreqs.Add(tf);
            foreach (var term in tf.Keys)
                docFreq[term] = docFreq.GetValueOrDefault(term) + 1;
        }

        _avgLen = docs.Count > 0 ? totalLen / docs.Count : 0;
        int n = docs.Count;
        foreach (var (term, df) in docFreq)
            _idf[term] = Math.Log(1 + (n - df + 0.5) / (df + 0.5));
    }

    /// <summary>Ranked matches for the query; empty/whitespace query returns the full catalog.</summary>
    public IReadOnlyList<SignInfo> Search(string? query)
    {
        var terms = Tokenize(query ?? string.Empty);
        if (terms.Count == 0)
            return _docs;

        var scored = new List<(SignInfo Doc, double Score)>();
        for (int i = 0; i < _docs.Count; i++)
        {
            var tf = _termFreqs[i];
            int docLen = tf.Values.Sum();
            double score = 0;
            foreach (var term in terms)
            {
                if (!tf.TryGetValue(term, out int freq)) continue;
                double idf = _idf.GetValueOrDefault(term);
                double denom = freq + K1 * (1 - B + B * (docLen / Math.Max(_avgLen, 1)));
                score += idf * (freq * (K1 + 1)) / denom;
            }
            if (score > 0)
                scored.Add((_docs[i], score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Select(s => s.Doc)
            .ToList();
    }

    [GeneratedRegex(@"\p{L}[\p{L}\p{M}]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        foreach (Match m in WordRegex().Matches(text.ToLowerInvariant()))
            tokens.Add(Stem(m.Value));
        return tokens;
    }

    // Very light stemmer: fold common English/Spanish plurals so "verbs" ≈ "verb".
    private static string Stem(string word) =>
        word.Length > 4 && (word.EndsWith("es") || word.EndsWith("s"))
            ? word.TrimEnd('s', 'e')
            : word;
}

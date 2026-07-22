using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core.Originality;

namespace SignsOfAI.Mcp.Tools;

/// <summary>Copy/originality tools: compare documents to each other, and extract phrases worth a web check. Offline.</summary>
[McpServerToolType]
public static class OriginalityTools
{
    private static readonly OriginalityChecker Checker = new();

    [McpServerTool(Name = "check_originality", ReadOnly = true),
     Description("""
        Compares two or more documents AGAINST EACH OTHER to find copied passages — a cohort of student
        submissions, a draft against its sources. For each document pair it returns the overlap percentage
        (case- and accent-insensitive) and the actual shared passages as evidence. This is NOT a whole-internet
        index like Turnitin; it only compares the documents you provide, fully offline. It surfaces evidence and
        lets a human judge — it never accuses.
        """)]
    public static OriginalityResult CheckOriginality(
        [Description("Two or more documents to compare. Each has an optional title and its text.")] DocumentInput[] documents)
    {
        if (documents is null || documents.Length < 2)
            throw new ArgumentException("Provide at least two documents to compare.");

        var inputs = documents
            .Select((d, i) => new OriginalityInput(
                (i + 1).ToString(),
                string.IsNullOrWhiteSpace(d.Title) ? $"Document {i + 1}" : d.Title!,
                d.Text ?? string.Empty))
            .ToList();

        var report = Checker.Check(inputs);

        var pairs = report.Pairs
            .Where(p => p.Overlap > 0)
            .Select(p => new PairOverlap(
                p.TitleA, p.TitleB,
                Math.Round(p.Overlap * 100, 1),
                Math.Round(p.Jaccard * 100, 1),
                p.SharedWords,
                p.LongestRunWords,
                p.Passages
                    .OrderByDescending(x => x.WordLength)
                    .Take(15)
                    .Select(x => Slice(inputs[p.IndexA].Text, x.SpanA.Start, x.SpanA.Length))
                    .Where(s => s.Length > 0)
                    .ToList()))
            .ToList();

        return new OriginalityResult(inputs.Count, pairs.Count, pairs);
    }

    [McpServerTool(Name = "extract_distinctive_phrases", ReadOnly = true),
     Description("""
        Extracts the most DISTINCTIVE phrases from a document — long, specific, proper-noun- or number-bearing
        wording most worth checking on the web — and returns each with ready-made exact-phrase search links
        (Google, Bing, DuckDuckGo). It does NOT search the web itself; it hands you the searches to run. Offline.
        """)]
    public static PhrasesResult ExtractDistinctivePhrases(
        [Description("The document text.")] string text,
        [Description("Maximum phrases to return. Default 8.")] int maxPhrases = 8)
    {
        var phrases = DistinctivePhraseExtractor.Extract(text ?? string.Empty, Math.Clamp(maxPhrases, 1, 25), 10);
        return new PhrasesResult(phrases.Select(p => new PhraseHit(p.Phrase, MakeSearchLinks(p.Phrase))).ToList());
    }

    private static string Slice(string s, int start, int len) =>
        start >= 0 && len >= 0 && start + len <= s.Length ? s.Substring(start, len) : string.Empty;

    private static SearchLinks MakeSearchLinks(string phrase)
    {
        var q = Uri.EscapeDataString("\"" + phrase + "\"");
        return new SearchLinks(
            $"https://www.google.com/search?q={q}",
            $"https://www.bing.com/search?q={q}",
            $"https://duckduckgo.com/?q={q}");
    }
}

public sealed record DocumentInput(string? Title, string Text);

public sealed record OriginalityResult(int DocumentCount, int OverlappingPairs, IReadOnlyList<PairOverlap> Pairs);

public sealed record PairOverlap(
    string DocumentA,
    string DocumentB,
    double OverlapPercent,
    double JaccardPercent,
    int SharedWords,
    int LongestSharedRunWords,
    IReadOnlyList<string> SharedPassages);

public sealed record PhrasesResult(IReadOnlyList<PhraseHit> Phrases);

public sealed record PhraseHit(string Phrase, SearchLinks Search);

public sealed record SearchLinks(string Google, string Bing, string DuckDuckGo);

using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core;

namespace SignsOfAI.Mcp.Tools;

/// <summary>The crown-jewel tool: run the AI-writing analyzer over some text. Fully offline.</summary>
[McpServerToolType]
public static class WritingTools
{
    // The analyzer is pure and stateless — one instance serves every call.
    private static readonly AiWritingAnalyzer Analyzer = new();

    [McpServerTool(Name = "analyze_ai_writing", ReadOnly = true),
     Description("""
        Analyzes text for the stylometric tells of AI writing (English & Spanish): overused vocabulary,
        rhetorical crutches, syntactic tells, and low burstiness (uniform sentence rhythm). Returns an overall
        0-100 "reads like AI" score, a plain-language verdict, per-category counts, document statistics, and a
        list of findings — each with the exact offending text, why it reads as AI, and an actionable fix.
        Runs fully offline; the text never leaves the machine. This is a signal, not proof of AI authorship.
        """)]
    public static AnalysisReport AnalyzeAiWriting(
        [Description("The text to analyze.")] string text,
        [Description("Language: \"en\", \"es\", or \"auto\" to detect. Default \"auto\".")] string language = "auto")
    {
        var r = Analyzer.Analyze(text ?? string.Empty, language);
        return new AnalysisReport(
            Math.Round(r.OverallScore, 1),
            r.Verdict,
            r.Language,
            r.Findings.Count,
            r.CategoryScores
                .Where(c => c.FindingCount > 0)
                .Select(c => new CategoryCount(c.Category.ToString(), c.FindingCount, Math.Round(c.Score, 1)))
                .ToList(),
            new DocStats(
                r.Statistics.WordCount,
                r.Statistics.SentenceCount,
                Math.Round(r.Statistics.MeanSentenceLength, 1),
                Math.Round(r.Statistics.Burstiness, 3),
                Math.Round(r.Statistics.LexicalDiversity, 3)),
            r.Findings
                .Select(f => new FindingItem(
                    f.Category.ToString(), f.Severity.ToString(), f.MatchedText, f.Message, f.Suggestion, f.Evidence))
                .ToList(),
            // A one-line pointer, not the report. Anything found here deserves the dedicated tool,
            // which returns coordinates; folding those into the score's payload would invite an
            // agent to treat a verifiable fact as one more contribution to a probability.
            r.Artifacts.Any
                ? new ArtifactNotice(r.Artifacts.Pattern.ToString(), r.Artifacts.Count,
                    r.Artifacts.StrongCount, r.Artifacts.Summary)
                : null,
            // Same treatment as the artifacts: a pointer, never the report. "check_citations" returns
            // the lines.
            r.Citations.ContradictionCount > 0
                ? new CitationNotice(r.Citations.ContradictionCount, r.Citations.References.Count,
                    r.Citations.Summary)
                : null);
    }
}

public sealed record AnalysisReport(
    double Score,
    string Verdict,
    string Language,
    int SignalCount,
    IReadOnlyList<CategoryCount> Categories,
    DocStats Statistics,
    IReadOnlyList<FindingItem> Findings,
    /// <summary>Null unless the text holds characters typing does not produce. See "inspect_characters".</summary>
    ArtifactNotice? CharacterArtifacts,
    /// <summary>Null unless the document contradicts its own reference list. See "check_citations".</summary>
    CitationNotice? SourceProblems);

/// <summary>
/// A flag that the text carries character artifacts, and nothing more — the score above is unaffected
/// by them, deliberately. Call "inspect_characters" for the codepoints and their positions.
/// </summary>
public sealed record ArtifactNotice(string Pattern, int Count, int StrongCount, string Summary);

/// <summary>
/// A flag that the document disagrees with its own bibliography. The score above is unaffected by it.
/// Call "check_citations" for the problems and their lines.
/// </summary>
public sealed record CitationNotice(int ContradictionCount, int ReferenceCount, string Summary);

public sealed record CategoryCount(string Category, int Count, double Score);

public sealed record DocStats(int Words, int Sentences, double MeanSentenceLength, double Burstiness, double LexicalDiversity);

public sealed record FindingItem(string Category, string Severity, string MatchedText, string Message, string Suggestion, string? Evidence);

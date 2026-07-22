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
                .ToList());
    }
}

public sealed record AnalysisReport(
    double Score,
    string Verdict,
    string Language,
    int SignalCount,
    IReadOnlyList<CategoryCount> Categories,
    DocStats Statistics,
    IReadOnlyList<FindingItem> Findings);

public sealed record CategoryCount(string Category, int Count, double Score);

public sealed record DocStats(int Words, int Sentences, double MeanSentenceLength, double Burstiness, double LexicalDiversity);

public sealed record FindingItem(string Category, string Severity, string MatchedText, string Message, string Suggestion, string? Evidence);

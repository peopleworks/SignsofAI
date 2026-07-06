using SignsOfAI.Core.Analyzers;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;
using SignsOfAI.Core.Scoring;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Core;

/// <summary>
/// The public entry point. Tokenizes the text, resolves the language rule-pack, runs every
/// analyzer, scores the result and returns findings each carrying an actionable suggestion.
/// </summary>
public sealed class AiWritingAnalyzer
{
    private readonly IReadOnlyList<IAnalyzer> _analyzers;

    /// <summary>Uses the default analyzer set (lexical + pattern + burstiness).</summary>
    public AiWritingAnalyzer() : this(DefaultAnalyzers()) { }

    public AiWritingAnalyzer(IReadOnlyList<IAnalyzer> analyzers) => _analyzers = analyzers;

    public static IReadOnlyList<IAnalyzer> DefaultAnalyzers() =>
    [
        new LexicalAnalyzer(),
        new PatternAnalyzer(),
        new BurstinessAnalyzer(),
    ];

    /// <param name="text">The text to analyze.</param>
    /// <param name="language">"en", "es", or null/"auto" to detect.</param>
    public AnalysisResult Analyze(string text, string? language = null)
    {
        text ??= string.Empty;

        var lang = language is null or "auto" or ""
            ? LanguageDetector.Detect(text)
            : language.ToLowerInvariant();

        var document = new TextDocument(text);
        var statistics = StatisticsCalculator.Compute(document);
        var rulePack = RulePackLoader.Load(lang);

        var context = new AnalysisContext
        {
            Document = document,
            Language = lang,
            RulePack = rulePack,
            Statistics = statistics,
        };

        var findings = _analyzers
            .SelectMany(a => a.Analyze(context))
            .OrderBy(f => f.Span.Start)
            .ThenBy(f => f.Span.Length)
            .ToList();

        var (overall, byCategory) = Scorer.Score(findings, statistics);

        return new AnalysisResult
        {
            Language = lang,
            Findings = findings,
            CategoryScores = byCategory,
            OverallScore = overall,
            Statistics = statistics,
        };
    }
}

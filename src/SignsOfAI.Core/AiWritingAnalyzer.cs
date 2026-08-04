using SignsOfAI.Core.Analyzers;
using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Citations;
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

    /// <summary>Uses the default analyzer set (lexical + pattern + burstiness + em-dash).</summary>
    public AiWritingAnalyzer() : this(DefaultAnalyzers()) { }

    public AiWritingAnalyzer(IReadOnlyList<IAnalyzer> analyzers) => _analyzers = analyzers;

    public static IReadOnlyList<IAnalyzer> DefaultAnalyzers() =>
    [
        new LexicalAnalyzer(),
        new PatternAnalyzer(),
        new BurstinessAnalyzer(),
        new EmDashAnalyzer(),
    ];

    /// <param name="text">The text to analyze.</param>
    /// <param name="language">"en", "es", or null/"auto" to detect.</param>
    /// <param name="extraPacks">
    /// Optional custom catalogs, merged on top of the built-in pack for the detected language.
    /// A pack applies when its <c>Language</c> matches (or is "*"/"all"/empty); rules override
    /// built-ins by id.
    /// </param>
    public AnalysisResult Analyze(string text, string? language = null, IReadOnlyList<RulePack>? extraPacks = null)
    {
        text ??= string.Empty;

        // Character artifacts are dealt with before anything else reads the text. A single Cyrillic
        // letter inside "delve" makes every word-matching rule miss while the page looks untouched,
        // so analysing the raw string would mean publishing a catalog anyone can switch off with a
        // find-and-replace. The analyzers run on the cleaned copy; the report describes the original.
        var probe = ArtifactScanner.Scan(text);
        var normalized = TextNormalizer.Apply(text, probe);

        var lang = language is null or "auto" or ""
            ? LanguageDetector.Detect(normalized.Text)
            : language.ToLowerInvariant();

        var document = new TextDocument(normalized.Text);
        var statistics = StatisticsCalculator.Compute(document);

        var rulePack = ResolvePack(lang, extraPacks);

        // Re-scanned only when there is something to report, this time with the pack that supplies
        // the wording — the first pass runs before the language is known.
        var artifacts = probe.Any ? ArtifactScanner.Scan(text, rulePack) : ArtifactReport.Empty;

        // Sources are read from the cleaned copy too, so a substituted letter cannot hide a citation
        // from its own bibliography any more than it can hide a word from the catalog.
        var citations = ToSource(CitationChecker.Check(normalized.Text, rulePack), normalized);

        var context = new AnalysisContext
        {
            Document = document,
            Language = lang,
            RulePack = rulePack,
            Statistics = statistics,
        };

        var matched = _analyzers
            .SelectMany(a => a.Analyze(context))
            .Select(f => normalized.Changed ? ToSource(f, normalized, text) : f)
            .OrderBy(f => f.Span.Start)
            .ThenBy(f => f.Span.Length)
            .ToList();

        // Rules that measured the genre rather than the machine are silenced here, against rates taken
        // from writing that predates generative models. Nothing is re-decided per finding: a rule is
        // either used at a human rate in this text, and says nothing, or it is not.
        var findings = GenreGate.Apply(matched, rulePack, statistics.WordCount);

        var (overall, byCategory) = Scorer.Score(findings, statistics);

        return new AnalysisResult
        {
            Language = lang,
            Findings = findings,
            CategoryScores = byCategory,
            OverallScore = overall,
            Statistics = statistics,
            Artifacts = artifacts,
            Citations = citations,
        };
    }

    /// <summary>
    /// The same report, with its spans expressed against the original text. Line numbers need no
    /// adjustment: cleaning removes characters and swaps letters, and neither adds or drops a newline.
    /// </summary>
    private static CitationReport ToSource(CitationReport report, NormalizedText normalized)
    {
        if (!normalized.Changed || !report.Any) return report;

        return report with
        {
            References = [.. report.References.Select(r => r with { Span = normalized.ToSource(r.Span) })],
            Citations = [.. report.Citations.Select(c => c with { Span = normalized.ToSource(c.Span) })],
            Issues = [.. report.Issues.Select(i => i with { Span = normalized.ToSource(i.Span) })],
        };
    }

    /// <summary>
    /// Re-expresses a finding against the text the reader has, rather than the cleaned copy the
    /// analyzers saw. The matched text is re-sliced too: a word that was found as "delve" should be
    /// shown the way it appears in the document, impostor letter included, because that is the thing
    /// the reader has to be able to find.
    /// </summary>
    private static Finding ToSource(Finding finding, NormalizedText normalized, string source)
    {
        var span = normalized.ToSource(finding.Span);
        return finding with
        {
            Span = span,
            MatchedText = span.Length > 0 && span.End <= source.Length
                ? span.Slice(source)
                : finding.MatchedText,
        };
    }

    /// <summary>
    /// The rule-pack an analysis of <paramref name="language"/> actually runs against: the built-in
    /// pack with any applicable custom catalogs merged over it.
    ///
    /// Public because a caller that wants to act on findings — the live rewriter needs each rule's
    /// replacements — has to consult the very same merged pack. Re-deriving it at the call site is how
    /// the two drift apart.
    /// </summary>
    public static RulePack ResolvePack(string language, IReadOnlyList<RulePack>? extraPacks = null)
    {
        var builtIn = RulePackLoader.Load(language);
        var applicable = extraPacks?.Where(p => p.AppliesTo(language)).ToList();
        return applicable is { Count: > 0 }
            ? RulePack.Merge(language, [builtIn, .. applicable])
            : builtIn;
    }
}

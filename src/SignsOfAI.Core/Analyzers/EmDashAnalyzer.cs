using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Analyzers;

/// <summary>
/// Flags over-reliance on the em-dash — an LLM's favorite rhythm crutch. A single em-dash is fine;
/// the tell is density. Emits one document-level finding (like burstiness) when em-dashes appear
/// far more often than human prose, which averages well under one per 100 words.
/// Counts real em-dashes (—), horizontal bars (―) and the "--" ASCII stand-in.
/// Categorised as Rhetorical so the signal counts toward the score, not just the report.
/// </summary>
public sealed class EmDashAnalyzer : IAnalyzer
{
    private const int MinWords = 40;       // too short to judge density
    private const int MinDashes = 3;       // a couple of dashes is normal
    private const double MinPer100 = 1.0;  // ~one em-dash per 100 words is already high for prose

    public SignCategory Category => SignCategory.Rhetorical;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        int words = context.Statistics.WordCount;
        if (words < MinWords)
            yield break;

        int dashes = CountEmDashes(context.Document.Raw);
        double per100 = dashes / (double)words * 100.0;
        if (dashes < MinDashes || per100 < MinPer100)
            yield break;

        var severity = per100 >= 3.0 ? Severity.High
            : per100 >= 1.6 ? Severity.Medium
            : Severity.Low;

        double weight = Math.Round(Math.Min(9.0, 2.0 + per100 * 2.0), 1);

        yield return new Finding
        {
            RuleId = "rhet.em-dash",
            Category = SignCategory.Rhetorical,
            Severity = severity,
            Span = new TextSpan(0, 0), // document-level
            MatchedText = string.Empty,
            Message = $"Em-dash overuse ({dashes} in {words} words, {per100:0.0}/100). " +
                      "LLMs lean on the em-dash as a rhythm crutch.",
            Suggestion = "Replace most with a period, comma, or parentheses; keep em-dashes rare and deliberate.",
            Evidence = "Human prose averages well under one em-dash per 100 words.",
            Weight = weight,
        };
    }

    private static int CountEmDashes(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '—' or '―') // em dash, horizontal bar
            {
                count++;
            }
            else if (c == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                count++;
                i++; // consume the pair, so "---" counts once
            }
        }
        return count;
    }
}

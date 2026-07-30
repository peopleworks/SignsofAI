using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Analyzers;

/// <summary>
/// The one statistical signal in the MVP: burstiness (coefficient of variation of sentence length).
/// LLMs default to uniform 15–25 word sentences → low burstiness. Humans vary wildly.
/// Emits a single document-level finding when uniformity is machine-like.
/// </summary>
public sealed class BurstinessAnalyzer : IAnalyzer
{
    // Below this, sentence length is suspiciously uniform. Research puts default LLM output at 0.0–0.2.
    private const double LowThreshold = 0.45;

    // Need enough sentences for the statistic to mean anything.
    private const int MinSentences = 4;

    public SignCategory Category => SignCategory.Statistical;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var stats = context.Statistics;
        if (stats.SentenceCount < MinSentences || stats.Burstiness >= LowThreshold)
            yield break;

        // Scale weight: the lower the burstiness, the stronger the signal (max ~12).
        double weight = Math.Round((LowThreshold - stats.Burstiness) / LowThreshold * 12.0, 1);
        var severity = stats.Burstiness < 0.25 ? Severity.High
            : stats.Burstiness < 0.35 ? Severity.Medium
            : Severity.Low;

        yield return new Finding
        {
            RuleId = "stat.burstiness",
            Category = SignCategory.Statistical,
            Severity = severity,
            Span = new TextSpan(0, 0), // document-level
            MatchedText = string.Empty,
            // Numbers are formatted before they reach the template so the pack holds words, not
            // format specifiers a translator would have to understand.
            Message = context.RulePack.Text(
                PackMessages.BurstinessMessage,
                stats.Burstiness.ToString("0.00"),
                stats.MeanSentenceLength.ToString("0.#")),
            Suggestion = context.RulePack.Text(PackMessages.BurstinessSuggestion),
            Evidence = context.RulePack.Text(PackMessages.BurstinessEvidence),
            Weight = weight,
        };
    }
}

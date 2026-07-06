using SignsOfAI.Core.Model;

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
            Message = $"Uniform sentence rhythm (burstiness {stats.Burstiness:0.00}, mean {stats.MeanSentenceLength:0.#} words). " +
                      "Machine-generated text tends to hold a steady 15–25 word cadence.",
            Suggestion = "Vary sentence length deliberately: follow a long, clause-heavy sentence with a short, punchy fragment. Let the rhythm breathe.",
            Evidence = "Human prose typically scores 0.6–0.8; default LLM output 0.0–0.2.",
            Weight = weight,
        };
    }
}

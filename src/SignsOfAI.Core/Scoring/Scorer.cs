using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Scoring;

/// <summary>
/// Turns findings + statistics into a 0–100 "reads like AI" score with a well-spread mid-range.
///
/// Two independent signals are combined:
///
/// 1. <b>Pattern density</b> — weighted lexical/rhetorical/syntactic findings per 100 words,
///    log-compressed and passed through a logistic curve. Calibrated so:
///      density ≈  5 /100w →  ~24   (light)
///      density ≈ 15 /100w →  ~50   (moderate)
///      density ≈ 40 /100w →  ~75   (strong)
///      density ≈ 100/100w →  ~90   (very strong; approaches but rarely reaches 100)
///
/// 2. <b>Burstiness</b> — scored directly from the sentence-length distribution, NOT folded into
///    per-word density. (Doing so was wrong: a fixed-weight document-level signal divided by word
///    count inflated on short texts and vanished on long ones — the opposite of where burstiness is
///    reliable.) Its contribution is scaled by confidence in the number of sentences.
///
/// The two are merged with a probabilistic OR so either can raise the score, but neither alone
/// pins it to 100. Zero findings and non-uniform rhythm yields exactly 0.
/// </summary>
public static class Scorer
{
    // Logistic parameters in log1p(density) space. Midpoint is where pattern density crosses 50.
    private const double Midpoint = 2.8;
    private const double Steepness = 1.15;

    // Treat very short inputs as at least this many words, so a single flagged word can't dominate.
    private const int WordFloor = 50;

    // Burstiness scoring.
    private const double BurstLowThreshold = 0.45;      // at/above this, rhythm looks human → 0
    private const int BurstMinSentences = 4;            // below this, the statistic is meaningless
    private const int BurstFullConfidenceSentences = 8; // full weight once we have this many
    private const double BurstMaxContribution = 75.0;   // cap so burstiness alone can't scream "AI"

    public static (double Overall, IReadOnlyList<CategoryScore> ByCategory) Score(
        IReadOnlyList<Finding> findings, TextStatistics stats)
    {
        int words = Math.Max(stats.WordCount, WordFloor);
        double burstiness = BurstinessScore(stats);

        var byCategory = new List<CategoryScore>();
        foreach (SignCategory category in Enum.GetValues<SignCategory>())
        {
            var group = findings.Where(f => f.Category == category).ToList();
            double score = category == SignCategory.Statistical
                ? burstiness
                : Map(group.Sum(f => f.Weight) / words * 100.0);
            byCategory.Add(new CategoryScore(category, Math.Round(score, 1), group.Count));
        }

        double patternWeight = findings
            .Where(f => f.Category != SignCategory.Statistical)
            .Sum(f => f.Weight);
        double patternScore = Map(patternWeight / words * 100.0);

        // Probabilistic OR: overall = 1 − (1−p)(1−b).
        double overall = 100.0 * (1.0 - (1.0 - patternScore / 100.0) * (1.0 - burstiness / 100.0));
        return (Math.Round(overall, 1), byCategory);
    }

    /// <summary>Maps weighted pattern density (per 100 words) to 0–100 with a spread mid-range.</summary>
    private static double Map(double density)
    {
        if (density <= 0) return 0;
        double x = Math.Log(1 + density);                       // compress the long tail
        return 100.0 / (1.0 + Math.Exp(-Steepness * (x - Midpoint)));
    }

    /// <summary>Scores sentence-length uniformity directly, weighted by confidence in sentence count.</summary>
    private static double BurstinessScore(TextStatistics stats)
    {
        if (stats.SentenceCount < BurstMinSentences || stats.Burstiness >= BurstLowThreshold)
            return 0;

        double raw = (BurstLowThreshold - stats.Burstiness) / BurstLowThreshold; // 0..1
        double confidence = Math.Min(1.0, stats.SentenceCount / (double)BurstFullConfidenceSentences);
        return raw * confidence * BurstMaxContribution;
    }
}

namespace SignsOfAI.Core.Calibration;

/// <summary>
/// Turns a pile of provably human writing into a statement the project can stand behind.
///
/// **Why a false-positive rate and not an accuracy figure.** Accuracy needs machine-written text to
/// measure against, and any collection of that is a sample of whichever models were convenient in
/// whichever month — a number that ages badly and flatters whoever assembled it. A false-positive
/// rate needs only writing known to be human, which does not go stale, and it measures the harm this
/// category actually does: detectors flag 61% of essays by non-native English speakers, and not one
/// of them publishes that figure about itself.
///
/// **Why the promise is made from the upper bound.** On fifty texts an observed 4% is honestly
/// anywhere between 1% and 13%. Recommending a threshold from the point estimate would be a decimal
/// point outrunning its evidence — the exact move this project refuses everywhere else. Taking the
/// upper end of the interval makes the recommendation conservative while the corpus is small and lets
/// it tighten on its own as the corpus grows, which is the right direction for the arrow to point.
///
/// Nothing here is specific to this project's own corpus. A school holding its students' pre-2022
/// work can calibrate on that instead, and get a false-positive rate for its own population rather
/// than somebody else's.
/// </summary>
public static class Calibrator
{
    /// <summary>95%, two-sided.</summary>
    private const double Z = 1.959964;

    /// <summary>Scores are 0–100, so whole numbers are a fine grid and read well in a table.</summary>
    public static IReadOnlyList<double> DefaultThresholds { get; } =
        [.. Enumerable.Range(1, 20).Select(i => i * 5.0)];

    public static CalibrationResult Compute(
        IReadOnlyList<CalibrationSample> samples,
        string corpusId,
        string corpusHash,
        double targetFalsePositiveRate = 0.05,
        IReadOnlyList<double>? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        thresholds ??= DefaultThresholds;

        return new CalibrationResult
        {
            CorpusId = corpusId,
            CorpusHash = corpusHash,
            TargetFalsePositiveRate = targetFalsePositiveRate,
            Overall = Measure("all", samples, thresholds, targetFalsePositiveRate),
            ByLanguage = [.. samples
                .GroupBy(s => s.Language, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => Measure(g.Key, [.. g], thresholds, targetFalsePositiveRate))],
            ByStratum = [.. samples
                .GroupBy(s => s.Stratum, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => Measure(g.Key, [.. g], thresholds, targetFalsePositiveRate))],
            RuleFalsePositives = RankRules(samples),
        };
    }

    /// <summary>
    /// One group's numbers. Every rule that fired inside this group fired on human writing, so every
    /// one of them is a false positive by construction — there is no judgement call to make.
    /// </summary>
    public static StratumCalibration Measure(
        string name,
        IReadOnlyList<CalibrationSample> samples,
        IReadOnlyList<double> thresholds,
        double target)
    {
        if (samples.Count == 0)
        {
            return new StratumCalibration
            {
                Name = name, Count = 0, TotalWords = 0,
                ShortestWords = 0, LongestWords = 0, MedianWords = 0,
                MedianScore = 0, NinetiethScore = 0, HighestScore = 0,
                Thresholds = [], ThresholdForTarget = null,
            };
        }

        var scores = samples.Select(s => s.Score).OrderBy(s => s).ToList();

        var rows = thresholds
            .OrderBy(t => t)
            .Select(t =>
            {
                int flagged = samples.Count(s => s.Score >= t);
                var (low, high) = WilsonInterval(flagged, samples.Count);
                return new ThresholdRow
                {
                    Threshold = t, Flagged = flagged, Total = samples.Count,
                    RateLow = low, RateHigh = high,
                };
            })
            .ToList();

        return new StratumCalibration
        {
            Name = name,
            Count = samples.Count,
            TotalWords = samples.Sum(s => s.WordCount),
            ShortestWords = samples.Min(s => s.WordCount),
            LongestWords = samples.Max(s => s.WordCount),
            MedianWords = Quantile([.. samples.Select(s => (double)s.WordCount).Order()], 0.50),
            MedianScore = Quantile(scores, 0.50),
            NinetiethScore = Quantile(scores, 0.90),
            HighestScore = scores[^1],
            Thresholds = rows,
            // The promise is made from the upper bound, so a thin corpus produces a cautious
            // threshold rather than a confident wrong one.
            ThresholdForTarget = rows.FirstOrDefault(r => r.RateHigh <= target)?.Threshold,
        };
    }

    /// <summary>
    /// Which rules misfire on human writing, most first. This is the most immediately useful thing
    /// the whole exercise produces: it turns "our rules probably have false positives somewhere" into
    /// a ranked list of which ones, on which kind of text, and how often.
    /// </summary>
    public static IReadOnlyList<RuleFalsePositive> RankRules(IReadOnlyList<CalibrationSample> samples)
    {
        if (samples.Count == 0) return [];

        var texts = new Dictionary<string, int>(StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var sample in samples)
        {
            foreach (var rule in sample.RuleIds)
                occurrences[rule] = occurrences.GetValueOrDefault(rule) + 1;

            foreach (var rule in sample.RuleIds.Distinct(StringComparer.Ordinal))
                texts[rule] = texts.GetValueOrDefault(rule) + 1;
        }

        return [.. texts
            .Select(kv => new RuleFalsePositive
            {
                RuleId = kv.Key,
                Texts = kv.Value,
                Occurrences = occurrences[kv.Key],
                TextShare = (double)kv.Value / samples.Count,
            })
            .OrderByDescending(r => r.TextShare)
            .ThenByDescending(r => r.Occurrences)
            .ThenBy(r => r.RuleId, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The Wilson score interval, rather than the textbook normal approximation.
    ///
    /// The usual one collapses exactly where this is used: at zero flagged out of forty it reports an
    /// interval of zero width, which would let the project claim a 0% false-positive rate from forty
    /// documents. Wilson stays honest at the edges, which is the only place that matters here.
    /// </summary>
    public static (double Low, double High) WilsonInterval(int successes, int total)
    {
        if (total == 0) return (0, 1);

        double p = (double)successes / total;
        double denominator = 1 + Z * Z / total;
        double centre = (p + Z * Z / (2.0 * total)) / denominator;
        double half = Z / denominator * Math.Sqrt(p * (1 - p) / total + Z * Z / (4.0 * total * total));

        return (Math.Max(0, centre - half), Math.Min(1, centre + half));
    }

    /// <summary>Linear-interpolated quantile over a sorted list.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 0) return 0;
        if (sorted.Count == 1) return sorted[0];

        double position = q * (sorted.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper) return sorted[lower];

        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }
}

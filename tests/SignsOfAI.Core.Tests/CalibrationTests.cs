using SignsOfAI.Core.Calibration;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// This is the arithmetic behind a number the project intends to publish about itself, so it is held
/// to the standard of a claim rather than of a helper. The tests that matter most are the ones about
/// not overclaiming: that zero flagged out of forty does not become "0% false positives", and that a
/// recommended threshold is derived from the uncertain end of the estimate rather than the flattering
/// one.
/// </summary>
public class CalibratorTests
{
    private static CalibrationSample Text(double score, string language = "en", string stratum = "native",
                                          params string[] rules) =>
        new() { Id = $"t{score}-{language}-{stratum}", Language = language, Stratum = stratum,
                Score = score, WordCount = 1000, RuleIds = rules };

    // ---- the interval ---------------------------------------------------------------------------

    [Fact]
    public void Nothing_flagged_is_not_a_promise_of_nothing()
    {
        // The textbook normal approximation returns a zero-width interval here, which would let this
        // project claim a 0% false-positive rate from forty documents. That claim would be false and
        // it is exactly the kind of thing people quote.
        var (low, high) = Calibrator.WilsonInterval(0, 40);

        Assert.Equal(0, low, 6);
        Assert.True(high > 0.05, $"upper bound collapsed to {high}");
        Assert.Equal(0.0876, high, 3);
    }

    [Fact]
    public void The_interval_matches_the_published_Wilson_formula()
    {
        var (low, high) = Calibrator.WilsonInterval(4, 50);

        Assert.Equal(0.0315, low, 3);
        Assert.Equal(0.1884, high, 3);
    }

    [Fact]
    public void An_empty_corpus_admits_everything()
    {
        var (low, high) = Calibrator.WilsonInterval(0, 0);

        Assert.Equal(0, low);
        Assert.Equal(1, high);
    }

    // ---- the recommended threshold ----------------------------------------------------------------

    [Fact]
    public void The_recommendation_is_made_from_the_uncertain_end_of_the_estimate()
    {
        // One flag in forty is 2.5%, comfortably under a 5% target. But the interval reaches 13%, so
        // the corpus cannot support the promise and no threshold is recommended. Reading the point
        // estimate instead would hand somebody a guarantee the evidence does not carry.
        var samples = new List<CalibrationSample> { Text(80) };
        samples.AddRange(Enumerable.Range(0, 39).Select(_ => Text(5)));

        var stratum = Calibrator.Measure("all", samples, [50.0], target: 0.05);
        var row = Assert.Single(stratum.Thresholds);

        Assert.Equal(0.025, row.Rate, 4);
        Assert.True(row.RateHigh > 0.05);
        Assert.Null(stratum.ThresholdForTarget);
    }

    [Fact]
    public void A_corpus_large_enough_and_clean_enough_earns_the_promise()
    {
        // With nothing flagged, the interval alone decides, and it needs roughly seventy-five texts
        // before it can bound a 5% rate. That number is worth knowing: it is what "enough corpus"
        // actually means here, and it is a great deal more than anybody assembles by accident.
        var samples = Enumerable.Range(0, 80).Select(_ => Text(5)).ToList();

        var stratum = Calibrator.Measure("all", samples, [50.0], target: 0.05);

        Assert.Equal(50.0, stratum.ThresholdForTarget);
    }

    [Fact]
    public void The_lowest_threshold_that_holds_is_the_one_recommended()
    {
        // A higher threshold flags less and is always safer; the useful answer is the lowest one that
        // still keeps the promise, because that is the most sensitive the tool can be set.
        var samples = Enumerable.Range(0, 100).Select(i => Text(i < 6 ? 60 : 5)).ToList();

        var stratum = Calibrator.Measure("all", samples, [20.0, 50.0, 70.0], target: 0.05);

        Assert.Equal(70.0, stratum.ThresholdForTarget);
    }

    // ---- shape ------------------------------------------------------------------------------------

    [Fact]
    public void Every_group_is_measured_separately()
    {
        // The whole reason for the exercise: a rate that holds for English and fails for Spanish, or
        // holds for native writers and fails for everyone else, is not one number and must not be
        // reported as one.
        var samples = new List<CalibrationSample>
        {
            Text(10, "en", "native"), Text(20, "en", "native"),
            Text(60, "es", "second-language"), Text(70, "es", "second-language"),
        };

        var result = Calibrator.Compute(samples, "test", "hash");

        Assert.Equal(4, result.Overall.Count);
        Assert.Equal(2, result.ByLanguage.Count);
        Assert.Equal(2, result.ByStratum.Count);
        Assert.Equal(2, Assert.Single(result.ByLanguage, s => s.Name == "es").Count);
        Assert.Equal(65, Assert.Single(result.ByStratum, s => s.Name == "second-language").MedianScore);
    }

    [Fact]
    public void Rules_that_misfire_on_human_writing_are_ranked()
    {
        // Every rule appearing here fired on text no machine wrote, so each is a false positive by
        // construction. This list is the most immediately actionable thing the exercise produces.
        var samples = new List<CalibrationSample>
        {
            Text(10, rules: ["rhet.em-dash", "rhet.em-dash", "lex.delve"]),
            Text(12, rules: ["rhet.em-dash"]),
            Text(14, rules: ["rhet.em-dash", "syn.rich-tapestry"]),
            Text(16, rules: []),
        };

        var ranked = Calibrator.RankRules(samples);

        Assert.Equal("rhet.em-dash", ranked[0].RuleId);
        Assert.Equal(3, ranked[0].Texts);
        Assert.Equal(4, ranked[0].Occurrences);   // counted twice in the first text
        Assert.Equal(0.75, ranked[0].TextShare, 4);
        Assert.Equal(3, ranked.Count);
    }

    [Fact]
    public void Quantiles_interpolate()
    {
        double[] sorted = [0, 10, 20, 30, 40];

        Assert.Equal(20, Calibrator.Quantile(sorted, 0.50), 6);
        Assert.Equal(36, Calibrator.Quantile(sorted, 0.90), 6);
        Assert.Equal(0, Calibrator.Quantile([], 0.5));
        Assert.Equal(7, Calibrator.Quantile([7.0], 0.9));
    }

    [Fact]
    public void An_empty_corpus_produces_a_result_that_says_nothing()
    {
        var result = Calibrator.Compute([], "empty", "hash");

        Assert.Equal(0, result.Overall.Count);
        Assert.Null(result.Overall.ThresholdForTarget);
        Assert.Empty(result.RuleFalsePositives);
        Assert.Empty(result.ByLanguage);
    }

    [Fact]
    public void A_threshold_flags_a_text_that_reaches_it()
    {
        // Boundary check, because the difference between > and >= here is somebody's essay.
        var samples = new List<CalibrationSample> { Text(50), Text(49.9) };

        var row = Assert.Single(Calibrator.Measure("all", samples, [50.0], 0.05).Thresholds);

        Assert.Equal(1, row.Flagged);
    }
}

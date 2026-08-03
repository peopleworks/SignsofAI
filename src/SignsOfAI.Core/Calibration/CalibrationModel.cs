namespace SignsOfAI.Core.Calibration;

/// <summary>
/// One text of known, human authorship, and what the analyzer said about it.
///
/// "Known human" is the whole basis of the exercise and it has to be earned rather than assumed. In
/// practice it means published before the models existed: a paper with a 2019 DOI was not written by
/// ChatGPT, and no amount of argument makes it so.
/// </summary>
public sealed record CalibrationSample
{
    public required string Id { get; init; }

    /// <summary>"en" or "es".</summary>
    public required string Language { get; init; }

    /// <summary>
    /// The group this text belongs to for reporting — the axis that matters here is whether the
    /// writer was working in a second language, because that is where every detector in this category
    /// does its damage and where none of them publish a number.
    /// </summary>
    public required string Stratum { get; init; }

    public required double Score { get; init; }

    public required int WordCount { get; init; }

    /// <summary>Every rule that fired on this human text — each one a false positive by construction.</summary>
    public required IReadOnlyList<string> RuleIds { get; init; }
}

/// <summary>
/// What share of provably human texts a given threshold would flag, with the uncertainty attached.
///
/// The interval is not decoration. On fifty texts an observed 4% could honestly be anything from 1%
/// to 13%, and publishing the 4% alone would be the same overclaiming this project refuses
/// everywhere else — a decimal point that outruns its evidence.
/// </summary>
public sealed record ThresholdRow
{
    public required double Threshold { get; init; }

    public required int Flagged { get; init; }

    public required int Total { get; init; }

    /// <summary>The observed share, which is a point estimate and nothing more.</summary>
    public double Rate => Total == 0 ? 0 : (double)Flagged / Total;

    /// <summary>Lower bound of the 95% Wilson interval.</summary>
    public required double RateLow { get; init; }

    /// <summary>Upper bound of the 95% Wilson interval — the number a promise should be made from.</summary>
    public required double RateHigh { get; init; }
}

/// <summary>Everything measured for one group of texts.</summary>
public sealed record StratumCalibration
{
    public required string Name { get; init; }

    public required int Count { get; init; }

    public required int TotalWords { get; init; }

    public required double MedianScore { get; init; }

    /// <summary>The score nine in ten of these human texts stay below.</summary>
    public required double NinetiethScore { get; init; }

    public required double HighestScore { get; init; }

    public required IReadOnlyList<ThresholdRow> Thresholds { get; init; }

    /// <summary>
    /// The lowest threshold whose *upper* confidence bound meets the target false-positive rate, or
    /// null when the corpus cannot support the promise at any threshold.
    ///
    /// Using the upper bound rather than the observed rate is what turns this from a hope into a
    /// statement: with few texts the bound is wide, so the recommended threshold comes out
    /// conservative, and it tightens on its own as the corpus grows. Null is a real and useful answer
    /// — it means "this corpus is too small to promise that", which is worth saying out loud.
    /// </summary>
    public double? ThresholdForTarget { get; init; }
}

/// <summary>A rule and how often it fired on writing no machine produced.</summary>
public sealed record RuleFalsePositive
{
    public required string RuleId { get; init; }

    public required int Texts { get; init; }

    public required int Occurrences { get; init; }

    /// <summary>Share of the human texts in which this rule fired at least once.</summary>
    public required double TextShare { get; init; }
}

/// <summary>
/// The result of measuring the analyzer against writing known to be human.
///
/// This is the answer to the question that has no answer today: "what is your accuracy?". It
/// deliberately does not produce one. Accuracy needs machine-written text to compare against, and any
/// collection of that would be a sample of whichever models were convenient in whichever month — a
/// number that ages badly and flatters whoever assembled it. A false-positive rate needs only human
/// writing, ages honestly, and measures the harm this category actually causes.
/// </summary>
public sealed record CalibrationResult
{
    /// <summary>Identifies the corpus this was measured on, so a claim can be reproduced.</summary>
    public required string CorpusId { get; init; }

    public required string CorpusHash { get; init; }

    public required double TargetFalsePositiveRate { get; init; }

    public required StratumCalibration Overall { get; init; }

    public required IReadOnlyList<StratumCalibration> ByLanguage { get; init; }

    public required IReadOnlyList<StratumCalibration> ByStratum { get; init; }

    /// <summary>Most frequent first — the rules to look at before writing any new ones.</summary>
    public required IReadOnlyList<RuleFalsePositive> RuleFalsePositives { get; init; }
}

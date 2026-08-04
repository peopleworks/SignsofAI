using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Core.Calibration;

/// <summary>
/// What this build of the analyzer knows about how often it is wrong, embedded so that anything it
/// produces can say so without a network call or a file path.
///
/// It exists because of the report. A document that accuses somebody, or that a teacher forwards to
/// a committee, has to carry its own error rate on the same page — otherwise the reader has a number
/// and no way to weigh it, which is the failure this whole project was built to argue against. Making
/// the report fetch that from a Markdown file, or worse restate it from memory in code, is how a page
/// ends up quoting a threshold three versions old.
///
/// Written by <c>tools/SignsOfAI.Calibration</c> in the same run that regenerates
/// <c>Docs/CALIBRATION.md</c>, so the two cannot disagree.
/// </summary>
public sealed record PublishedCalibration
{
    /// <summary>The corpus this was measured against, e.g. "signsofai-human-baseline".</summary>
    public required string CorpusId { get; init; }

    /// <summary>How many texts, all published before generative models could have written them.</summary>
    public required int Texts { get; init; }

    /// <summary>Date of the measuring run, yyyy-MM-dd.</summary>
    public required string MeasuredOn { get; init; }

    /// <summary>The engine version that produced it.</summary>
    public required string Engine { get; init; }

    /// <summary>
    /// The lowest score at which the upper bound of the false-positive interval stays inside the
    /// target. Null when the corpus is too small to support any threshold, which must be said rather
    /// than papered over with a plausible-looking number.
    /// </summary>
    public double? RecommendedThreshold { get; init; }

    /// <summary>How many of <see cref="Texts"/> were flagged at <see cref="RecommendedThreshold"/>.</summary>
    public int FlaggedAtThreshold { get; init; }

    /// <summary>
    /// The 95% Wilson bounds on that rate, as fractions. The upper one is the number that means
    /// something: zero out of ninety is not a zero per cent false-positive rate, and a report that
    /// implies it would be making exactly the overclaim this project criticises in everyone else.
    /// </summary>
    public double RateLow { get; init; }

    public double RateHigh { get; init; }

    /// <summary>
    /// The rules most often seen on human writing, worst first. Printed alongside a report's findings
    /// so a reader can see whether the evidence they are holding leans on a rule that is known to be
    /// noisy.
    /// </summary>
    public IReadOnlyList<PublishedRuleRate> NoisiestRules { get; init; } = [];

    private static PublishedCalibration? _current;

    /// <summary>
    /// The calibration shipped with this build, or null if none was embedded — which is a legitimate
    /// state for a fork that has not measured itself, and callers must handle it by saying so rather
    /// than by omitting the caveat.
    /// </summary>
    public static PublishedCalibration? Current
    {
        get
        {
            if (_current is not null) return _current;

            var assembly = typeof(PublishedCalibration).Assembly;
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("published-calibration.json", StringComparison.Ordinal));
            if (name is null) return null;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) return null;

            return _current = JsonSerializer.Deserialize(
                stream, PublishedCalibrationJsonContext.Default.PublishedCalibration);
        }
    }
}

/// <summary>One rule and how often it appeared in writing no machine wrote.</summary>
public sealed record PublishedRuleRate
{
    public required string RuleId { get; init; }

    /// <summary>Share of the corpus it fired on, as a fraction.</summary>
    public required double TextShare { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PublishedCalibration))]
public partial class PublishedCalibrationJsonContext : JsonSerializerContext;

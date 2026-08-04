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

    /// <summary>
    /// The same measurement per language, and the reason this type is not a single number.
    ///
    /// `Docs/CALIBRATION.md` puts it plainly one line above its own table: "a rate that holds in
    /// English and fails in Spanish is not one number, and reporting it as one would hide exactly the
    /// failure that matters here." On the current corpus the English bound is 5.6% and the Spanish one
    /// 13.3%, and neither group is large enough to support the 5% target on its own. Quoting the
    /// aggregate on a report about a Spanish essay would hand its author a bound more than three times
    /// better than anything measured for their language.
    /// </summary>
    public IReadOnlyList<PublishedLanguage> Languages { get; init; } = [];

    /// <summary>What was measured for one language, or null when that language was not in the corpus.</summary>
    public PublishedLanguage? For(string? language) =>
        language is null
            ? null
            : Languages.FirstOrDefault(l => string.Equals(l.Language, language, StringComparison.OrdinalIgnoreCase));

    private static PublishedCalibration? _current;
    private static bool _loaded;

    /// <summary>
    /// The calibration shipped with this build, or null if none was embedded or it could not be read —
    /// a legitimate state for a fork that has not measured itself, which callers must handle by saying
    /// so rather than by omitting the caveat.
    ///
    /// It never throws. A malformed resource — a fork's hand edit, a merge conflict marker, an
    /// encoding slip — would otherwise take down every report, every CLI run and every button in the
    /// interface, from a property getter, over a caveat. Failing to a page that admits it has no
    /// measured error rate is strictly better than failing to no page at all.
    ///
    /// The result is cached including the null, so a missing resource does not rescan the manifest on
    /// every analysis. The race is benign: two threads may both read the resource and one assignment
    /// wins, and the value is immutable either way.
    /// </summary>
    public static PublishedCalibration? Current
    {
        get
        {
            if (_loaded) return _current;

            try
            {
                var assembly = typeof(PublishedCalibration).Assembly;
                var name = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("published-calibration.json", StringComparison.Ordinal));

                if (name is not null)
                {
                    using var stream = assembly.GetManifestResourceStream(name);
                    if (stream is not null)
                        _current = JsonSerializer.Deserialize(
                            stream, PublishedCalibrationJsonContext.Default.PublishedCalibration);
                }
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or IOException)
            {
                _current = null;
            }

            _loaded = true;
            return _current;
        }
    }
}

/// <summary>
/// One language's slice of the corpus. <see cref="RecommendedThreshold"/> is null when this group is
/// too small to bound its own rate — which must be said out loud, because it is the honest answer and
/// the aggregate is not.
/// </summary>
public sealed record PublishedLanguage
{
    public required string Language { get; init; }

    public required int Texts { get; init; }

    public double? RecommendedThreshold { get; init; }

    /// <summary>The best upper bound this group can support, as a fraction.</summary>
    public required double BestBound { get; init; }
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

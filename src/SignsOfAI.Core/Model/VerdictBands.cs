using SignsOfAI.Core.Calibration;

namespace SignsOfAI.Core.Model;

/// <summary>
/// Where the verdict changes, and the only place that decides it.
///
/// It used to be decided in eight: the analysis result, the report, the interface's localiser, the
/// CLI's colour picker, two switches in the web page, the batch page and the live-rewrite panel —
/// each with the numbers written out again, kept in step by a comment reading "mirrors the bands in
/// AnalysisResult.Verdict". They did not stay in step, and the drift was not cosmetic: the report
/// withheld the verdict from every document ever analysed while the interface and the CLI printed
/// one for every document ever analysed. One engine gave three answers about the same text.
/// </summary>
public static class VerdictBands
{
    /// <summary>
    /// The score at which this build is willing to say something about a text, taken from the
    /// calibration it ships with rather than chosen.
    ///
    /// Null when no calibration is embedded — a fork that has never measured itself. That case must
    /// stay silent rather than inherit a boundary somebody else measured on somebody else's corpus,
    /// which is the same rule the report already follows for the error rate.
    /// </summary>
    public static double? Threshold => PublishedCalibration.Current?.RecommendedThreshold;

    /// <summary>
    /// Whether a score has earned a verdict at all. Below the boundary the number stands alone: a
    /// low score is not evidence that a person wrote something, and the wording must not imply it.
    /// </summary>
    public static bool Holds(double score) => Threshold is { } threshold && score >= threshold;

    /// <summary>
    /// The same question for a document in a named language, which is stricter and has to be.
    ///
    /// There are three states here, not two, and collapsing them is how this went wrong before:
    ///
    /// <list type="bullet">
    /// <item>A language <b>in the corpus</b> — English, Spanish — has a measured false-positive bound
    /// of its own, even when its sample is too small to set its own boundary. It borrows the pooled
    /// boundary and the page prints its bound beside the verdict, so the reader weighs the right
    /// number.</item>
    /// <item>A language <b>absent from the corpus</b> has no bound to print. A verdict there would
    /// imply a reliability nobody measured, and there would be nothing on the page to correct the
    /// impression. It gets the score and the reason it gets nothing else.</item>
    /// <item>No calibration at all — a fork that has never measured itself — speaks about nothing.</item>
    /// </list>
    /// </summary>
    public static bool Holds(double score, string? language) => Holds(score) && Measured(language);

    /// <summary>Whether the corpus contains this language at all, however thinly.</summary>
    public static bool Measured(string? language) =>
        PublishedCalibration.Current?.For(language) is not null;

    /// <summary>
    /// How loudly to present a score that has earned a verdict.
    ///
    /// The two upper cuts are a **display convention and nothing more**. No text in the calibration
    /// corpus came within twenty points of them — the highest scoring human text reached 23.4 — so
    /// the corpus can locate <see cref="Threshold"/> and can say nothing whatever about 45 or 70.
    /// Separating "moderate" from "strong" would need machine-written text, and Docs/CALIBRATION.md
    /// argues at length against collecting any: it dates badly and flatters whoever assembles it.
    ///
    /// They survive here to colour a reading, never to make a claim. See issue #32.
    /// </summary>
    public static VerdictEmphasis Emphasis(double score) => score switch
    {
        _ when !Holds(score) => VerdictEmphasis.None,
        >= 70 => VerdictEmphasis.High,
        >= 45 => VerdictEmphasis.Elevated,
        _ => VerdictEmphasis.Present,
    };
}

/// <summary>
/// How prominently a verdict is shown. Not a measurement, and deliberately not a number: anything
/// that reads as a quantity here would be read as one that was measured, and only the boundary
/// between <see cref="None"/> and the rest was.
/// </summary>
public enum VerdictEmphasis
{
    /// <summary>Below the boundary this build can support. The score stands without a verdict.</summary>
    None,

    Present,

    Elevated,

    High,
}

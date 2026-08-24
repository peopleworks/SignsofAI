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
    /// The shortest text the boundary was ever measured on, or null when the embedded calibration
    /// predates this field.
    ///
    /// This is a statement about **coverage, not about reliability**. It does not claim the tool
    /// breaks below this length; it says nothing was measured there, which is a different and much
    /// weaker claim — and the only one the corpus can support. The 25/100 boundary was fitted on 90
    /// texts whose shortest is 662 words and whose median is 2,772, and it was being applied to a
    /// pasted paragraph with nothing on the page to say so. See issue #59.
    ///
    /// The number comes from the analyzer's own word count, not from a naive split on whitespace —
    /// the two disagree by about 7% on this corpus. Deriving the floor with one counter and comparing
    /// against another would silently rescale the gate, which is the same trap
    /// <see cref="Rules.GenreGate"/> documents for its rates.
    ///
    /// It is an observation rather than a fitted parameter, and that is the whole point of choosing
    /// it: no grid of lengths, no windows cut out of longer documents, no subset selected to make a
    /// number come out. Every earlier attempt at this measured a rate against synthetic short text
    /// and inherited the problem it was fixing — a 400-word window sliced out of a paper is not a
    /// paragraph somebody *composed* at 400 words, and a floor fitted on the first does not describe
    /// the second.
    ///
    /// The way to lower it is to measure shorter writing: complete texts, published before 2022, at
    /// the lengths people actually paste. That is issue #66, and every one of them extends this
    /// downward by evidence rather than by decision.
    /// </summary>
    public static int? MinimumWords => PublishedCalibration.Current?.ShortestWords;

    /// <summary>
    /// Whether a document of this length sits inside what the boundary was measured on.
    ///
    /// **There is deliberately no ceiling.** The asymmetry is measured, not assumed: shortening a
    /// text moves its score toward the machine — 0 of 32 documents flagged whole, 6 of the same 32
    /// flagged as 400-word excerpts of themselves (`Docs/PARAPHRASE.md`, section *Length*) — while
    /// nothing suggests a thesis longer than the corpus is at risk. Silencing the long end too would
    /// withhold a verdict for a symmetry nobody has evidence for.
    ///
    /// A null minimum does **not** gate. That is deliberately unlike the language condition, where
    /// absence from the corpus is a positive fact each snapshot records. Here null means the snapshot
    /// is older than the field, not that the corpus had no lengths; going silent on it would stop
    /// every fork carrying a 0.4.0 snapshot from speaking at all, which is a change driven by a
    /// missing field rather than by evidence.
    /// </summary>
    public static bool Measured(int wordCount) =>
        MinimumWords is not { } floor || wordCount >= floor;

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

    /// <summary>
    /// The full question, and the one every surface should ask: score, language and length together.
    ///
    /// The three conditions are the same rule applied three times — *a bound measured on one
    /// population must not be spent on another* — and they are answered in one place because the
    /// last time this was decided in eight, one engine gave three answers about the same text.
    /// </summary>
    public static bool Holds(double score, string? language, int wordCount) =>
        Holds(score, language) && Measured(wordCount);

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

    /// <summary>
    /// The same, for a document whose language and length are known.
    ///
    /// Colour is part of the verdict whatever the design system pretends. A page that withholds a
    /// verdict in words and paints the score red anyway has given the verdict — louder, and without
    /// the sentence that qualifies it.
    /// </summary>
    public static VerdictEmphasis Emphasis(double score, string? language, int wordCount) =>
        !Measured(wordCount) || !Measured(language) ? VerdictEmphasis.Unmeasured
        : Emphasis(score);
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

    /// <summary>
    /// Outside what was measured — a language the corpus never contained, or a document shorter than
    /// anything the boundary was fitted on.
    ///
    /// Separate from <see cref="None"/>, and the separation is the point. Both withhold a verdict, but
    /// <see cref="None"/> is a reading — the tool looked and found little, and a reassuring colour is
    /// honest for it. This one is a refusal to read, and painting a 72/100 passage green because the
    /// verdict was withheld would state the opposite of what was withheld, in the loudest channel on
    /// the page. See issue #59.
    /// </summary>
    Unmeasured,
}

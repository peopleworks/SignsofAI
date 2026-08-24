using System.Linq;
using SignsOfAI.Core;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Reporting;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The length condition on the verdict — issue #59.
///
/// The boundary this build ships was fitted on 90 texts whose shortest is 662 words, and it was being
/// applied to a pasted paragraph with nothing on the page to say so. The same documents flag 0 of 32
/// whole and 6 of 32 as 400-word excerpts of themselves, so the error does not merely get noisier as
/// text gets shorter: it moves one way, toward the machine.
///
/// What these guard is a **coverage** claim, not a reliability one. Nothing here asserts that the
/// tool is wrong below 662 words. It asserts that the tool stops claiming, which is the only thing
/// the corpus supports — and that it goes on showing the evidence, because the evidence was never
/// what the boundary was about.
/// </summary>
public class VerdictLengthTests
{
    private readonly AiWritingAnalyzer _analyzer = new();

    /// <summary>A paragraph with enough tells to score well over the boundary, and far too short.</summary>
    private const string ShortAndMachineLike =
        "In today's rapidly evolving digital landscape, we must delve into the rich tapestry of " +
        "innovation. It is worth noting that this multifaceted approach serves as a testament to " +
        "human ingenuity. It's not just a tool, it's a pivotal, transformative solution that " +
        "fosters growth, unlocks potential, and empowers teams. Moreover, the seamless integration " +
        "underscores a paradigm shift, highlighting the profound implications for the realm of " +
        "modern work.";

    [Fact]
    public void The_floor_is_the_shortest_text_the_boundary_was_measured_on()
    {
        var published = PublishedCalibration.Current;
        Assert.NotNull(published);

        // Published rather than chosen. If this is ever null while a threshold is published, the
        // build is claiming a boundary without recording the population it was fitted on.
        Assert.NotNull(published!.ShortestWords);
        Assert.Equal(published.ShortestWords, VerdictBands.MinimumWords);
        Assert.True(published.ShortestWords < published.LongestWords,
            "A range whose ends are equal is not a range; the snapshot is malformed.");
    }

    [Fact]
    public void A_paragraph_gets_no_verdict_however_high_it_scores()
    {
        var result = _analyzer.Analyze(ShortAndMachineLike, "en");

        Assert.True(result.OverallScore >= VerdictBands.Threshold,
            "The fixture stopped scoring above the boundary, so this test no longer proves anything.");
        Assert.True(result.Statistics.WordCount < VerdictBands.MinimumWords);

        Assert.False(result.HasVerdict);
        Assert.Equal("No verdict: below the measured length", result.Verdict);
    }

    [Fact]
    public void The_same_writing_at_length_does_get_one()
    {
        var result = _analyzer.Analyze(Fixtures.LongEnough(ShortAndMachineLike), "en");

        Assert.True(result.Statistics.WordCount >= VerdictBands.MinimumWords);
        Assert.True(result.HasVerdict);
        Assert.Equal("Signs of AI writing", result.Verdict);
    }

    /// <summary>
    /// The verdict is withheld; the evidence is not. Suppressing the findings too would leave a
    /// teacher with a shorter answer than they had before, and the findings never depended on the
    /// boundary — "delve" is in the text or it is not.
    /// </summary>
    [Fact]
    public void Withholding_the_verdict_does_not_withhold_the_evidence()
    {
        var result = _analyzer.Analyze(ShortAndMachineLike, "en");

        Assert.False(result.HasVerdict);
        Assert.NotEmpty(result.Signals);
        Assert.Contains(result.Findings, f => f.RuleId == "lex.delve");
        Assert.True(result.OverallScore > 0, "The score is still computed and still shown.");
    }

    /// <summary>
    /// Two different silences, two different sentences. "Below the threshold" is a reading — the tool
    /// looked and found little. "Shorter than anything measured" is a refusal to read. A reader told
    /// the first when the second is true takes away a reassurance nobody offered.
    /// </summary>
    [Fact]
    public void The_report_says_which_silence_it_is()
    {
        var shortReport = EvidenceReport.ToMarkdown(_analyzer.Analyze(ShortAndMachineLike, "en"));

        Assert.Contains("measured only on texts of", shortReport);
        Assert.DoesNotContain("A low score is not evidence that a person wrote this", shortReport);
        Assert.DoesNotContain("Signs of AI writing", shortReport);

        // And it names both numbers, so the reader can check the claim rather than take it.
        Assert.Contains(VerdictBands.MinimumWords!.Value.ToString("N0"), shortReport);
    }

    [Fact]
    public void A_spanish_reader_is_told_the_same_thing_in_Spanish()
    {
        var report = EvidenceReport.ToMarkdown(
            _analyzer.Analyze(ShortAndMachineLike, "en"),
            new ReportOptions { InterfaceLanguage = "es" });

        Assert.Contains("se midió solo sobre textos de", report);
        Assert.DoesNotContain("measured only on texts of", report);
    }

    /// <summary>
    /// Colour is part of the verdict whatever the design system pretends. Before this, a withheld
    /// verdict fell through to <see cref="VerdictEmphasis.None"/>, which every surface paints green —
    /// so a 72/100 passage would have been withheld in words and certified in colour.
    /// </summary>
    [Fact]
    public void An_unmeasured_document_is_not_painted_as_a_clean_one()
    {
        var result = _analyzer.Analyze(ShortAndMachineLike, "en");

        Assert.Equal(
            VerdictEmphasis.Unmeasured,
            VerdictBands.Emphasis(result.OverallScore, result.Language, result.Statistics.WordCount));

        // And the plain overload, which knows nothing about length, still says what it always said —
        // it is not the one to ask, and callers that ask it are the ones this test cannot catch.
        Assert.NotEqual(VerdictEmphasis.Unmeasured, VerdictBands.Emphasis(result.OverallScore));
    }

    [Fact]
    public void A_document_longer_than_the_corpus_is_still_judged()
    {
        // No ceiling, and the asymmetry is measured rather than assumed: shortening a text moves its
        // score toward the machine, and nothing suggests a long thesis is at risk. Silencing the long
        // end for symmetry would withhold a verdict for a reason nobody has evidence for.
        Assert.True(VerdictBands.Measured(int.MaxValue));
        Assert.True(VerdictBands.Measured(PublishedCalibration.Current!.LongestWords!.Value * 10));
    }

    /// <summary>
    /// One place decides. Before <see cref="VerdictBands"/> existed the answer was written out in
    /// eight, and one engine gave three answers about the same text; length must not reintroduce a
    /// second opinion.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void Every_way_of_asking_agrees(string language)
    {
        var text = language == "es"
            ? "En el panorama actual, cabe destacar que este enfoque integral no solo optimiza los " +
              "procesos sino que también facilita una comprensión robusta del fenómeno."
            : ShortAndMachineLike;

        var result = _analyzer.Analyze(text, language);
        var speaks = result.HasVerdict;

        Assert.Equal(speaks, VerdictBands.Holds(result.OverallScore, result.Language, result.Statistics.WordCount));
        Assert.Equal(speaks, result.Verdict == "Signs of AI writing");
        Assert.Equal(
            speaks,
            EvidenceReport.ToMarkdown(result).Contains("**" + System.Math.Round(result.OverallScore) + "/100 —"));
    }
}

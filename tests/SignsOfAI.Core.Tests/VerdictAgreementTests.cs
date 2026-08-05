using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Reporting;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// One text, one engine, one answer.
///
/// Nothing in this suite used to notice that the three surfaces disagreed, which is why they were
/// allowed to disagree for weeks. The exported report withheld the verdict from every document ever
/// analysed — its gate demanded a per-language threshold, and no language in the corpus has one —
/// while the CLI and the web interface printed a hand-picked band on every document ever analysed.
/// A text scoring 90/100 was called "Strong signs of AI writing" on screen and given no verdict at
/// all in the document a teacher would print and carry to a committee.
///
/// These tests exist so that the next disagreement fails a build instead of shipping.
/// </summary>
public class VerdictAgreementTests
{
    /// <summary>Dense with the tells the packs describe: a score at the top of the scale.</summary>
    private const string ObviouslyMachine =
        "In today's rapidly evolving digital landscape, it is important to note that artificial " +
        "intelligence has fundamentally transformed how we approach problem-solving. Moreover, this " +
        "comprehensive framework facilitates a robust understanding of the underlying mechanisms. " +
        "Furthermore, it is worth noting that such systems not only enhance productivity but also " +
        "streamline operations. In order to leverage these capabilities, organizations must delve " +
        "into the intricacies of implementation. Additionally, the multifaceted nature of these " +
        "tools underscores their pivotal role.";

    [Fact]
    public void The_report_speaks_about_a_text_the_product_calls_the_strongest_case_it_has()
    {
        var result = new AiWritingAnalyzer().Analyze(ObviouslyMachine, "en");

        // Not a claim that the score is right — only that a document scoring this high is the case
        // the tool exists for. If the report stays silent here it stays silent everywhere.
        Assert.True(result.OverallScore >= 70,
            $"The fixture stopped being an extreme case (scored {result.OverallScore:0}); " +
            "pick a stronger one rather than lowering this bar.");

        var report = EvidenceReport.ToMarkdown(result);

        Assert.DoesNotContain("no verdict is given", report);
    }

    [Fact]
    public void No_language_is_silenced_merely_because_its_own_corpus_is_small()
    {
        // The per-language threshold needs roughly seventy-five texts. Spanish has twenty-five and
        // will not reach seventy-five for a long time. A gate that waits for it does not protect a
        // Spanish writer; it withholds the tool from them while serving everyone else — and on this
        // corpus it withholds it from everyone, because English has sixty-five.
        var calibration = PublishedCalibration.Current;
        Assert.NotNull(calibration);

        foreach (var language in calibration!.Languages)
            Assert.True(language.RecommendedThreshold is null,
                $"'{language.Language}' now supports its own threshold. That is good news and it " +
                "makes this test's premise obsolete — keep the fallback, but re-read it.");

        var spanish = new AiWritingAnalyzer().Analyze(
            "En el panorama actual, es importante destacar que la inteligencia artificial ha " +
            "transformado fundamentalmente nuestro enfoque. Además, este marco integral facilita " +
            "una comprensión robusta de los mecanismos subyacentes. Asimismo, cabe señalar que " +
            "dichos sistemas no solo mejoran la productividad sino que también optimizan las " +
            "operaciones.", "es");

        var report = EvidenceReport.ToMarkdown(spanish);

        // Whatever it says about Spanish, it must not be silence justified by a missing number that
        // the aggregate already supplies.
        Assert.Contains("13.3%", report);
    }

    [Fact]
    public void The_boundary_the_product_draws_is_the_boundary_the_project_publishes()
    {
        // The product used to say "light signs of AI writing" from 20, five points below the only
        // threshold this project publishes, and below the highest-scoring human text in the corpus
        // (23.4). Publishing a measured figure while shipping an unmeasured boundary is the
        // inconsistency this project exists to complain about in other tools.
        var threshold = PublishedCalibration.Current?.RecommendedThreshold;
        Assert.NotNull(threshold);

        Assert.Equal(threshold!.Value, VerdictBands.Threshold);
    }
}

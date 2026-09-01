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
    public void A_language_too_small_for_its_own_threshold_still_gets_a_report_that_names_its_own_bound()
    {
        // The per-language threshold needs roughly seventy-five texts. English crossed that line in
        // September 2026 with the learner essays; Spanish has twenty-five and will not reach it for a
        // long time. A gate that waits for it does not protect a Spanish writer; it withholds the tool
        // from them while serving everyone else. So a language without its own threshold borrows the
        // pooled boundary and carries its own best bound beside it, and this checks the second half.
        var calibration = PublishedCalibration.Current;
        Assert.NotNull(calibration);

        var borrowing = calibration!.Languages.Where(l => l.RecommendedThreshold is null).ToList();
        Assert.True(borrowing.Count > 0,
            "Every language now supports its own threshold. That is good news and it makes this " +
            "test's premise obsolete — keep the fallback, but re-read it.");

        foreach (var language in borrowing)
        {
            var report = EvidenceReport.ToMarkdown(new AiWritingAnalyzer().Analyze(
                SampleIn(language.Language), language.Language));

            // Whatever it says, it must not be silence justified by a missing number that the
            // aggregate already supplies — and the bound it quotes must be this language's own.
            Assert.Contains($"{language.BestBound * 100:0.0}%", report);
        }
    }

    private static string SampleIn(string language) => language switch
    {
        "es" => "En el panorama actual, es importante destacar que la inteligencia artificial ha " +
                "transformado fundamentalmente nuestro enfoque. Además, este marco integral facilita " +
                "una comprensión robusta de los mecanismos subyacentes. Asimismo, cabe señalar que " +
                "dichos sistemas no solo mejoran la productividad sino que también optimizan las " +
                "operaciones.",
        "en" => "In today's landscape, it is important to note that artificial intelligence has " +
                "fundamentally transformed our approach. Moreover, this comprehensive framework " +
                "facilitates a robust understanding of the underlying mechanisms. Additionally, " +
                "these systems not only improve productivity but also optimize operations.",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language,
                "A language joined the calibration; give this test a sample in it."),
    };

    [Fact]
    public void The_verdict_reaches_its_reader_in_their_own_language()
    {
        // Rewording the English retires the SHA-256 pin every translation records for it, and a
        // translation whose pin no longer matches is treated as stale — the whole report silently
        // falls back to English. That is the correct behaviour and a silent way to undo #36, so the
        // wording change and the pins have to travel together. This is the test that says they did.
        // Long enough to earn a verdict at all: since #59 a paragraph gets none, and this test is
        // about which language the verdict arrives in, not about whether one is given.
        var result = new AiWritingAnalyzer().Analyze(Fixtures.LongEnough(ObviouslyMachine), "en");
        var spanish = EvidenceReport.ToMarkdown(result, new ReportOptions { InterfaceLanguage = "es" });

        Assert.Contains("Señales de escritura con IA", spanish);
        Assert.DoesNotContain("Signs of AI writing", spanish);
    }

    [Fact]
    public void A_low_score_says_nothing_about_who_wrote_the_text()
    {
        // "Reads mostly human" in the report, "Minimal signs of AI writing" in the interface: the
        // same state, two claims, and the first was never ours to make. A detector that detects
        // nothing also returns zero, and this project has deliberately never measured how much
        // machine writing it catches.
        var human = new AiWritingAnalyzer().Analyze(
            "We measured height, weight and age in three hundred participants recruited at two " +
            "hospitals during the winter of 2011. At one site the effect vanished entirely.", "en");

        Assert.False(VerdictBands.Holds(human.OverallScore),
            $"The fixture stopped being a low-scoring text (scored {human.OverallScore:0}).");

        Assert.DoesNotContain("human", human.Verdict, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("person", human.Verdict, StringComparison.OrdinalIgnoreCase);
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

using SignsOfAI.Core;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Reporting;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The report is the artefact a teacher forwards to a student or takes to a committee, so the tests
/// that matter are about what it must always carry and what it must never imply.
/// </summary>
public class EvidenceReportTests
{
    private const string Essay = """
        Formative assessment improves retention, particularly where feedback is returned quickly.
        Delgado and Ruiz (2021) report gains across two cohorts, and Whitfield (2020) finds a similar
        effect. In terms of the mechanism, Ashworth (2019) argues it is mediated by self-efficacy.

        ## References

        Ashworth, P. (2019). Self-efficacy and feedback. Journal of Educational Measurement, 57(2),
        143-161. https://doi.org/10.1080/jem.2019.4471

        Delgado, M., & Ruiz, C. (2021). Formative assessment and retention. Studies in Higher
        Education, 46(4), 512-538. https://doi.org/10.1080/jem.2019.4471
        """;

    private static string Report(string text = Essay) =>
        EvidenceReport.ToMarkdown(new AiWritingAnalyzer().Analyze(text, "en"));

    [Fact]
    public void Always_states_how_often_the_tool_is_wrong()
    {
        // The one thing that must never be droppable. A document that accuses somebody without saying
        // how often the method errs is the artefact this project argues against.
        var report = Report();

        Assert.Contains("A score is not proof", report);
        Assert.Contains("How often this is wrong", report);
    }

    [Fact]
    public void Quotes_the_interval_rather_than_the_observed_rate()
    {
        // Zero out of ninety is not a false-positive rate of zero, and a report that implied it would
        // be making the overclaim this project criticises in everyone else.
        var report = Report();

        Assert.Contains("upper bound", report);
        Assert.Contains("Read the interval, not the observed rate", report);

        var published = PublishedCalibration.Current;
        Assert.NotNull(published);
        Assert.Contains($"{published.Texts} texts published before generative models existed", report);
    }

    [Fact]
    public void Separates_checkable_facts_from_the_score()
    {
        // A DOI on two different works is not an opinion about prose, and must not read as one.
        var report = Report();

        Assert.Contains("Checkable facts", report);
        Assert.Contains("did not move the score", report);
        Assert.Contains("10.1080/jem.2019.4471", report);
        Assert.Contains("cited in the text but appears nowhere in the reference list", report);
    }

    [Fact]
    public void Says_it_holds_the_document_and_was_never_uploaded()
    {
        // The share card is built to be posted in public; this is not. Confusing them would publish a
        // student's work, so the difference is stated on the page rather than assumed.
        var report = Report();

        Assert.Contains("nothing here was uploaded anywhere", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Names_the_rules_known_to_misfire_so_the_reader_can_weigh_the_evidence()
    {
        var report = Report();
        var published = PublishedCalibration.Current;

        Assert.NotNull(published);
        Assert.NotEmpty(published.NoisiestRules);
        Assert.Contains(published.NoisiestRules[0].RuleId, report);
    }

    [Fact]
    public void The_html_is_self_contained_and_carries_no_script()
    {
        // It has to open from a downloads folder years later on a machine with no network, and a
        // report that fetches anything would leak which documents were analysed and when.
        var html = EvidenceReport.ToHtml(new AiWritingAnalyzer().Analyze(Essay, "en"));

        Assert.StartsWith("<!doctype html>", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("src=", html);
        Assert.DoesNotContain("href=", html);
    }

    [Fact]
    public void Markup_in_the_document_cannot_escape_into_the_page()
    {
        var html = EvidenceReport.ToHtml(
            new AiWritingAnalyzer().Analyze("A comprehensive <script>alert(1)</script> overview.", "en"),
            new ReportOptions { DocumentName = "<img onerror=alert(1)>" });

        Assert.DoesNotContain("<script>alert", html);
        Assert.DoesNotContain("<img onerror", html);
        Assert.Contains("&lt;", html);
    }

    [Fact]
    public void Distinguishes_what_counted_from_what_was_merely_present()
    {
        var body = string.Join(" ", Enumerable.Repeat(
            "The study examined the data carefully and reported what it found.", 160));

        var report = EvidenceReport.ToMarkdown(
            new AiWritingAnalyzer().Analyze(body + " Furthermore, the result held.", "en"));

        Assert.Contains("at a rate people write at", report);
        Assert.Contains("lex.furthermore", report);
    }
}

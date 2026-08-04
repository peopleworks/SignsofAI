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

        Assert.Contains("upper end of a 95% interval", report);
        Assert.Contains("not a guarantee", report);
        Assert.Contains("Read the interval, not the observed rate", report);

        // Never the bare rate dressed as a promise. Scoped to the claim about this tool: "at most"
        // is fine elsewhere on the page — a DOI issue legitimately says at most one of two entries
        // can be right — and a blanket ban would be a test about wording rather than about honesty.
        Assert.DoesNotContain("flags at most", report);

        // Skipped rather than asserted when no snapshot is embedded: a fork that has never measured
        // itself is a state the design blesses, and a test suite that fails for it would push people
        // toward shipping somebody else's number.
        if (PublishedCalibration.Current is { } published)
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
        if (PublishedCalibration.Current is { NoisiestRules.Count: > 0 } published)
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

    [Fact]
    public void The_folder_report_says_it_is_a_reading_order_and_not_a_ranking()
    {
        // A table of students sorted by score, with nothing else on the page, reads like a ranking of
        // guilt. That sentence is the difference between a triage tool and an accusation generator.
        var report = EvidenceReport.FolderToMarkdown("Essays/Week 1",
        [
            new FolderEntry("ana.docx", 1200, 61, 7, null),
            new FolderEntry("luis.docx", 900, 12, 1, null),
            new FolderEntry("broken.pdf", null, null, null, "Encrypted"),
        ]);

        Assert.Contains("reading order, not a ranking", report);
        Assert.Contains("Nothing on this page establishes that anyone did anything", report);
        Assert.Contains("A score is not proof", report);
        Assert.Contains("How often this is wrong", report);
    }

    [Fact]
    public void The_folder_report_puts_the_highest_score_first_and_keeps_unreadable_files()
    {
        var report = EvidenceReport.FolderToMarkdown("Essays",
        [
            new FolderEntry("low.docx", 900, 12, 1, null),
            new FolderEntry("high.docx", 1200, 61, 7, null),
            new FolderEntry("broken.pdf", null, null, null, "Encrypted"),
        ]);

        Assert.True(report.IndexOf("high.docx", StringComparison.Ordinal)
                    < report.IndexOf("low.docx", StringComparison.Ordinal));
        Assert.Contains("Could not be read", report);
        Assert.Contains("broken.pdf — Encrypted", report);
    }

    [Fact]
    public void A_pipe_in_a_filename_cannot_shift_the_columns()
    {
        // Legal in a filename on Linux and macOS, and the folder table is where a teacher reads scores
        // against student names. An extra cell moves every number one column right.
        var html = EvidenceReport.FolderToHtml("Essays",
            [new FolderEntry("weird|name.txt", 10, 50, 2, null)]);

        var row = html.Split('\n').First(l => l.Contains("weird"));
        Assert.Equal(4, row.Split("<td>").Length - 1);
        Assert.Contains("weird|name.txt", row);
    }

    [Fact]
    public void A_newline_in_user_content_cannot_forge_a_heading()
    {
        // Extractor errors and rule-pack matches are user content, and MarkdownToHtml dispatches on
        // the first characters of a line. A forged heading in the report's own voice, in a document
        // that goes to a committee, is worse than a broken layout.
        var html = EvidenceReport.FolderToHtml("Essays",
            [new FolderEntry("b.txt", null, null, null, "Line one\n## Forged heading")]);

        Assert.DoesNotContain("<h2>Forged heading</h2>", html);
        Assert.Contains("Forged heading", html);
    }

    [Fact]
    public void Says_nothing_about_contradictions_when_the_cross_checks_did_not_run()
    {
        // A document with citations and no reference list. The first version announced that it
        // "contradicts itself" directly under a line saying the checks had not been run.
        const string noBibliography = """
            Formative assessment improves retention. Delgado and Ruiz (2021) report gains across two
            cohorts, and Whitfield (2020) finds a similar effect in a larger sample.
            """;

        var report = EvidenceReport.ToMarkdown(new AiWritingAnalyzer().Analyze(noBibliography, "en"));

        Assert.DoesNotContain("contradicts itself", report);
        Assert.DoesNotContain("disagrees with itself", report);
    }

    [Fact]
    public void The_folder_report_lists_every_file_rather_than_the_first_forty()
    {
        // The document a teacher keeps after closing the app. Dropping a hundred and sixty of two
        // hundred — every low scorer among them — would be worse than not writing it.
        var entries = Enumerable.Range(1, 200)
            .Select(i => new FolderEntry($"essay-{i:000}.docx", 900, i % 70, 1, null))
            .ToList();

        var report = EvidenceReport.FolderToMarkdown("Essays", entries);

        Assert.Contains("essay-200.docx", report);
        Assert.Contains("essay-001.docx", report);
    }

    [Fact]
    public void A_file_that_failed_to_read_is_not_also_in_the_reading_order()
    {
        var report = EvidenceReport.FolderToMarkdown("Essays",
            [new FolderEntry("odd.docx", 10, 90, 3, "Encrypted")]);

        Assert.Contains("Could not be read", report);
        Assert.DoesNotContain("| odd.docx |", report);
    }
}

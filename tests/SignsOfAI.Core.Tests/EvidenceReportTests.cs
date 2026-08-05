using SignsOfAI.Core;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Reporting;
using System.Text.RegularExpressions;

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

        // Either wording is honest; what must never happen is a page with neither. Which one appears
        // depends on whether the analysed language has enough corpus to support a threshold at all.
        Assert.True(report.Contains("A score is not proof")
                    || report.Contains("No threshold is supported"));
        Assert.Contains("How often this is wrong", report);
    }

    [Fact]
    public void Quotes_the_interval_rather_than_the_observed_rate()
    {
        // Zero out of ninety is not a false-positive rate of zero, and a report that implied it would
        // be making the overclaim this project criticises in everyone else.
        var report = Report();

        // When the language does support a threshold the caveat quotes the interval, never a promise;
        // when it does not, it says so and refuses to borrow the aggregate. Both are asserted because
        // which one appears is a property of the corpus, not of this code.
        if (report.Contains("A score is not proof"))
        {
            Assert.Contains("upper end of a 95% interval", report);
            Assert.Contains("not a guarantee", report);
        }
        else
        {
            Assert.Contains("the overall figure is not a substitute for it", report);
            Assert.DoesNotContain("Read the interval, not the observed rate", report);
        }

        // Never the bare rate dressed as a promise. Scoped to the claim about this tool: "at most"
        // is fine elsewhere on the page — a DOI issue legitimately says at most one of two entries
        // can be right — and a blanket ban would be a test about wording rather than about honesty.
        Assert.DoesNotContain("flags at most", report);

        // Skipped rather than asserted when no snapshot is embedded: a fork that has never measured
        // itself is a state the design blesses, and a test suite that fails for it would push people
        // toward shipping somebody else's number. When present, only the analysed-language stratum
        // may be named here — never the aggregate.
        if (PublishedCalibration.Current?.For("en") is { } english)
            Assert.Contains($"{english.Texts} texts in this language", report);
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

    [Fact]
    public void Never_quotes_a_bound_measured_on_another_language()
    {
        // The calibration page says it one line above its own table: a rate that holds in English and
        // fails in Spanish is not one number. Quoting the aggregate on a Spanish essay would hand its
        // author a bound measured mostly on English.
        var report = EvidenceReport.ToMarkdown(new AiWritingAnalyzer().Analyze(
            "La evaluación formativa mejora la retención estudiantil, y cabe destacar que el efecto " +
            "parece mediado por la autoeficacia del estudiante más que por la retroalimentación.", "es"));

        if (PublishedCalibration.Current?.For("es") is { } spanish)
            Assert.Contains($"{spanish.Texts} texts", report);
    }

    [Fact]
    public void An_unmeasured_language_gets_no_aggregate_threshold_or_verdict()
    {
        var analysed = new AiWritingAnalyzer().Analyze(Essay, "en") with
        {
            Language = "pt",
            OverallScore = 100,
        };

        var report = EvidenceReport.ToMarkdown(analysed);

        Assert.Contains("never been measured for language code pt", report);
        Assert.Contains("aggregate result from other languages is not a substitute", report);
        Assert.DoesNotContain("Strong signs of AI writing", report);

        if (PublishedCalibration.Current is { } calibration)
        {
            Assert.DoesNotContain($"{calibration.Texts} texts published", report);
            if (calibration.RecommendedThreshold is { } threshold)
                Assert.DoesNotContain($"{threshold:0.#}/100", report);
        }
    }

    [Fact]
    public void Report_structure_follows_the_interface_not_the_analysed_language()
    {
        var result = new AiWritingAnalyzer().Analyze(Essay, "en");
        var options = new ReportOptions { InterfaceLanguage = "es" };

        var markdown = EvidenceReport.ToMarkdown(result, options);
        var html = EvidenceReport.ToHtml(result, options);

        Assert.Contains("# Informe del análisis de escritura", markdown);
        Assert.Contains("## Qué dice el análisis", markdown);
        Assert.Contains("## Con qué frecuencia se equivoca", markdown);
        Assert.Contains("Este informe contiene", markdown);
        Assert.Contains("Este bloque aún no está traducido", markdown);
        Assert.Contains("<html lang=\"es\">", html);

        var stated = int.Parse(Regex.Match(markdown,
            @"Este informe contiene (\d+) bloque").Groups[1].Value);
        var marked = Regex.Matches(markdown,
            "Este bloque aún no está traducido").Count;
        Assert.Equal(marked, stated);
        Assert.True(markdown.IndexOf("Este informe contiene", StringComparison.Ordinal)
                    < markdown.IndexOf("Este bloque aún no está traducido", StringComparison.Ordinal));
    }

    [Fact]
    public void A_report_language_without_the_mandatory_core_is_rejected()
    {
        var result = new AiWritingAnalyzer().Analyze(Essay, "en");

        // Rejected as a language the report can be *written in* — never as a reason to withhold the
        // report. An incomplete translation must not be able to destroy the evidence: the page is
        // served in English and says so, which is a worse read and an honest one.
        var report = EvidenceReport.ToMarkdown(result, new ReportOptions { InterfaceLanguage = "pt" });

        Assert.Contains("not available in", report);

        // The caveat still has to be on the page. Which of the two wordings appears is a property of
        // the corpus, not of this code, so both are accepted and neither may be absent.
        Assert.True(report.Contains("A score is not proof")
                    || report.Contains("No threshold is supported"));
    }

    [Fact]
    public void Withholds_the_verdict_below_the_threshold_it_can_support()
    {
        // "Reads mostly human" printed above "treat the score as saying nothing" is a page arguing
        // with itself, and the reader keeps whichever half suits them.
        var report = Report();

        Assert.DoesNotContain("Reads mostly human", report);
        Assert.Contains("A low score is not evidence that a person wrote this", report);
    }

    [Fact]
    public void A_language_it_cannot_write_in_still_produces_the_report()
    {
        // The first version threw. A teacher holding two hundred essays and a button that does
        // nothing has lost the evidence entirely, which is the outcome this feature exists to
        // prevent — and the MCP tool takes the interface language as free text from a model, so
        // any unknown string would have crashed the tool rather than degraded the page.
        var result = new AiWritingAnalyzer().Analyze(Essay, "en");

        var report = EvidenceReport.ToMarkdown(result, new ReportOptions { InterfaceLanguage = "fr" });

        Assert.Contains("not available in", report);
        Assert.Contains("How often this is wrong", report);
    }

    [Fact]
    public void Says_it_is_serving_English_rather_than_serving_it_quietly()
    {
        // Silent fallback is the failure this project criticises in everyone else: a page that looks
        // complete while withholding the part that limits it.
        var result = new AiWritingAnalyzer().Analyze(Essay, "en");

        var report = EvidenceReport.ToMarkdown(result, new ReportOptions { InterfaceLanguage = "pt" });

        Assert.Contains("shown in English", report);
        Assert.Contains("pt", report);
    }

    [Fact]
    public void A_supported_interface_language_carries_no_apology()
    {
        var result = new AiWritingAnalyzer().Analyze(Essay, "en");

        var report = EvidenceReport.ToMarkdown(result, new ReportOptions { InterfaceLanguage = "es" });

        Assert.DoesNotContain("not available in", report);
        Assert.DoesNotContain("no está disponible en", report);
    }

    [Fact]
    public void Puts_the_checkable_facts_in_the_headline()
    {
        // The document this project keeps writing about scores near zero and has an invented
        // bibliography. Burying that below the number would be choosing the wrong thing to make
        // salient, on the one page where it matters most.
        var report = Report();

        Assert.Contains("Checkable facts found:", report);
        Assert.Contains("source contradiction", report);
        Assert.True(report.IndexOf("Checkable facts found:", StringComparison.Ordinal)
                    < report.IndexOf("## Checkable facts", StringComparison.Ordinal));
    }
}

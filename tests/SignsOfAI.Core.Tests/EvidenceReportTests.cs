using SignsOfAI.Core;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Citations;
using SignsOfAI.Core.Model;
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
        Assert.DoesNotContain("Signs of AI writing", report);

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
        // A verdict printed above "treat the score as saying nothing" is a page arguing with itself,
        // and the reader keeps whichever half suits them. Below the boundary the report prints the
        // score and the reason, and no verdict line of either kind.
        // Long enough that the reason for the silence is the threshold and not the length — the two
        // are different sentences since #59, and this test is about the first one.
        var report = EvidenceReport.ToMarkdown(
            new AiWritingAnalyzer().Analyze(Fixtures.LongEnough(Essay), "en"));

        Assert.DoesNotContain("No signs above the measured boundary", report);
        Assert.DoesNotContain("Signs of AI writing", report);
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

    /// <summary>
    /// A result assembled by hand. The ordering defects need a document with more findings than the
    /// report prints and a known strongest one, and building that out of real prose would make the
    /// test a statement about which rules happen to fire rather than about what the report keeps.
    /// </summary>
    private static AnalysisResult Synthetic(
        IReadOnlyList<Finding> findings, IReadOnlyList<CitationIssue>? issues = null) => new()
    {
        Language = "en",
        RulePackLanguage = "en",
        Findings = findings,
        CategoryScores = [],
        OverallScore = 30,
        Statistics = new TextStatistics { WordCount = 4000, SentenceCount = 200 },
        Citations = issues is null ? CitationReport.Empty : new CitationReport
        {
            References = [],
            // One citation, because CitationReport.Any gates the whole section on there being
            // something to describe — a report with issues and no sources is not a real state.
            Citations =
            [
                new InTextCitation
                {
                    Raw = "(Delgado & Ruiz, 2021)",
                    Span = new TextSpan(0, 22),
                    Line = 1,
                    Surname = "Delgado",
                    Year = 2021,
                },
            ],
            Issues = issues,
            Style = CitationStyle.AuthorYear,
            HasReferenceList = true,
            Summary = "A bibliography with one contradiction and a great deal of untidiness.",
            Advice = "Ask about the repeated identifier.",
            ContradictionCount = issues.Count(i => i.IsContradiction),
        },
    };

    private static Finding Signal(string id, double weight, int start, string matched) => new()
    {
        RuleId = id,
        Category = SignCategory.Lexical,
        Severity = Severity.Medium,
        Span = new TextSpan(start, matched.Length),
        MatchedText = matched,
        Message = "Reads as a machine tell.",
        Suggestion = "Say it another way.",
        Weight = weight,
    };

    [Fact]
    public void The_strongest_evidence_survives_a_document_longer_than_the_report()
    {
        // The defect this replaces: findings arrive in the order they occur in the text, the report
        // cut the list at forty, and so a long document spent the whole list on weak hits in its
        // opening pages while the finding that did most to produce the headline number — in the last
        // paragraph — was dropped. The reader was handed a score the visible evidence could not
        // account for, in the document this project builds for a room where somebody is judged.
        var findings = Enumerable.Range(0, 60)
            .Select(i => Signal("lex.weak", 1.0, i * 100, $"weak-{i:000}"))
            .Append(Signal("lex.strong", 9.0, 99_000, "unmistakable-tell"))
            .ToList();

        var report = EvidenceReport.ToMarkdown(Synthetic(findings));

        Assert.Contains("unmistakable-tell", report);
        Assert.DoesNotContain("weak-059", report);
    }

    [Fact]
    public void Says_what_it_left_out_and_that_none_of_it_outweighed_what_is_shown()
    {
        // "… and 21 more" over a list in document order says the report stopped reading. Over a list
        // in weight order it can say something stronger and true: there is more, and none of it is
        // heavier than what you are looking at. The claim is only earned by sorting first.
        var findings = Enumerable.Range(0, 61)
            .Select(i => Signal("lex.weak", 1.0, i * 100, $"weak-{i:000}"))
            .ToList();

        var report = EvidenceReport.ToMarkdown(Synthetic(findings));

        Assert.Contains("Ordered by how much weight each one carries", report);
        Assert.Contains("21 more, none of them carrying more weight than what is shown above", report);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void A_row_limit_below_one_cannot_invent_omitted_evidence(int maxRows)
    {
        // MaxRows is public, settable and unvalidated, and this library is on NuGet. The omission
        // count is Count minus the limit, so a negative limit reported more omitted findings than
        // the document had; and at zero rows the strong clause claimed nothing omitted outweighed
        // what was shown, with nothing shown.
        var findings = Enumerable.Range(0, 2)
            .Select(i => Signal("lex.weak", 1.0, i * 100, $"weak-{i:000}"))
            .ToList();

        var report = EvidenceReport.ToMarkdown(Synthetic(findings),
            new ReportOptions { MaxRows = maxRows });

        Assert.Contains("… and 2 more.", report);
        Assert.DoesNotContain("carrying more weight than what is shown above", report);
    }

    [Fact]
    public void A_contradiction_is_not_pushed_out_of_the_list_by_untidiness()
    {
        // The same defect as the signals list, left in place one commit ago. The checker draws the
        // line itself with IsContradiction, and a bibliography can carry forty uncited entries and
        // one repeated DOI — the finding that settles anything — in its last one.
        var issues = Enumerable.Range(0, 45)
            .Select(i => new CitationIssue
            {
                Kind = CitationIssueKind.ListedButNotCited,
                Span = new TextSpan(i * 10, 5),
                Line = i + 1,
                Subject = $"Entry {i}",
                Message = $"untidy-{i:000} is listed but never cited.",
                IsContradiction = false,
            })
            .Append(new CitationIssue
            {
                Kind = CitationIssueKind.RepeatedDoi,
                Span = new TextSpan(9_000, 5),
                Line = 999,
                Subject = "10.1080/jem.2019.4471",
                Message = "the-same-doi on two different works.",
                IsContradiction = true,
            })
            .ToList();

        var report = EvidenceReport.ToMarkdown(Synthetic([], issues));

        Assert.Contains("the-same-doi", report);
        Assert.DoesNotContain("untidy-044", report);
        Assert.Contains("… and 6 more.", report);
    }

    [Fact]
    public void An_untidy_bibliography_is_not_announced_as_a_contradiction()
    {
        // People legitimately list further reading, which is why CitationIssue draws the line itself
        // with IsContradiction. The headline counted every issue instead, so a document whose only
        // fault is an uncited entry was announced as "1 source contradiction" over a note asserting
        // it "disagrees with itself" — an accusation the checker had explicitly declined to make,
        // on the page a teacher takes to a committee.
        const string tidy = """
            Formative assessment improves retention. Delgado and Ruiz (2021) report gains across two
            cohorts, and the effect held after a year.

            ## References

            Delgado, M., & Ruiz, C. (2021). Formative assessment and retention. Studies in Higher
            Education, 46(4), 512-538.

            Whitfield, J. (2020). Feedback timing in large cohorts. Assessment Review, 12(1), 33-51.
            """;

        var result = new AiWritingAnalyzer().Analyze(tidy, "en");
        var report = EvidenceReport.ToMarkdown(result);

        // Guard the premise: the fixture must actually produce an untidiness and no contradiction,
        // or the assertions below would pass without testing anything.
        Assert.Contains(result.Citations.Issues, i => !i.IsContradiction);
        Assert.Equal(0, result.Citations.ContradictionCount);

        Assert.DoesNotContain("source contradiction", report);
        Assert.DoesNotContain("disagrees with itself", report);
    }

    [Fact]
    public void A_spanish_reader_is_told_the_order_in_Spanish()
    {
        // The line that explains why the list is not in the order of their student's document is the
        // one a reader most needs in their own language, and a stale pin would drop it back to
        // English without saying anything was wrong with it.
        var findings = Enumerable.Range(0, 61)
            .Select(i => Signal("lex.weak", 1.0, i * 100, $"weak-{i:000}"))
            .ToList();

        var report = EvidenceReport.ToMarkdown(Synthetic(findings),
            new ReportOptions { InterfaceLanguage = "es" });

        Assert.Contains("Ordenadas por el peso que carga cada una", report);
        Assert.Contains("21 más, ninguna con más peso que las que se muestran arriba", report);
    }

    [Fact]
    public void A_strong_character_is_not_pushed_out_of_the_table_by_soft_hyphens()
    {
        // Word inserts soft hyphens unprompted, so a real file can hold hundreds of them and one
        // letter borrowed from another alphabet. In file order the innocent ones fill the table and
        // the one occurrence that is hard to arrive at by accident falls off the end — of the table
        // this project points at when it says a character is a fact rather than an opinion.
        var text = string.Concat(Enumerable.Repeat("sepa­ration of the parts. ", 60))
                   + "The final delveе stands alone.";

        var report = EvidenceReport.ToMarkdown(new AiWritingAnalyzer().Analyze(text, "en"));

        Assert.Contains("Letter from another alphabet", report);
    }

    [Fact]
    public void The_markdown_form_does_not_carry_live_html_into_a_comment_box()
    {
        // Markdown is the form documented here for pasting into an LMS comment box or a GitHub issue,
        // and both render raw HTML embedded in Markdown. ToHtml escaped on its way out so the HTML
        // page was never at risk; the Markdown was, and it is the one a teacher is told to forward.
        var findings = new[] { Signal("lex.x", 3.0, 0, "<img src=x onerror=alert(1)>") };

        var markdown = EvidenceReport.ToMarkdown(Synthetic(findings));

        // Neutralised where a renderer reads it, and still legible where a person does: the escape
        // is a backslash, so the teacher reading the raw file sees what the document actually said.
        Assert.Contains(@"\<img src=x onerror=alert(1)>", markdown);

        // And no bracket that opens a tag survived unescaped anywhere on the page.
        Assert.DoesNotContain("<img", markdown.Replace(@"\<", ""));
    }

    [Fact]
    public void The_document_cannot_put_an_image_in_the_report()
    {
        // Escaping the angle bracket blocks raw HTML and does nothing to Markdown's own image
        // syntax. The report states on its own last line that nothing here was uploaded anywhere;
        // a page that fetches a pixel from someone else's host the moment a teacher pastes it into
        // the LMS has broken that sentence, and the fetch carries the moment it was opened.
        var findings = new[] { Signal("lex.x", 3.0, 0, "![tracking](https://attacker.invalid/pixel)") };

        var markdown = EvidenceReport.ToMarkdown(Synthetic(findings));

        Assert.Contains(@"!\[tracking](https://attacker.invalid/pixel)", markdown);

        // Strip the escapes this file writes; no bracket that opens an image or a link is left.
        Assert.DoesNotContain("[", markdown.Replace(@"\[", ""));
    }

    [Fact]
    public void An_asterisk_in_the_document_cannot_capture_the_report_s_own_prose()
    {
        // The row is written as: matched text, then the report's suggestion wrapped in *…*. A single
        // asterisk in the document paired with the report's own marker, so emphasis opened at the
        // document's character and closed at the report's — swallowing the report's explanation into
        // the document's content, and deleting the matched character on the way.
        var findings = new[] { Signal("lex.x", 3.0, 0, "*") };

        var html = EvidenceReport.ToHtml(Synthetic(findings));

        // &#42; rather than a bare asterisk, and that is the fix rather than a compromise: it is a
        // literal asterisk to every renderer — exactly as &lt; is a literal angle bracket — and it
        // is not a character the emphasis pass can mistake for a marker.
        Assert.Contains("“&#42;”", html);
        Assert.Contains("<em>→ Say it another way.</em>", html);
    }

    [Fact]
    public void A_language_code_from_a_host_cannot_carry_markup()
    {
        // The MCP server takes language strings as free text from a model, and the public analyzer
        // API takes one from any host. It reaches the page through "language code {0}".
        var result = Synthetic([]) with { Language = "<img src=x onerror=alert(1)>" };

        var markdown = EvidenceReport.ToMarkdown(result);

        Assert.DoesNotContain("<img", markdown.Replace(@"\<", ""));
    }

    [Fact]
    public void Report_metadata_cannot_forge_a_second_report()
    {
        // DocumentName was routed through Cell and these two were not, though all three are public
        // settable strings on the same record.
        var markdown = EvidenceReport.ToMarkdown(Synthetic([]),
            new ReportOptions
            {
                GeneratedOn = "2026-08-10\n\n## The tool concludes the student cheated",
                EngineVersion = "1.0\n\n## And this line is not ours either",
            });

        Assert.DoesNotContain("\n## The tool concludes", markdown);
        Assert.DoesNotContain("\n## And this line", markdown);
    }

    [Fact]
    public void The_matched_text_keeps_the_spaces_the_rule_matched()
    {
        // A rule whose regex includes the surrounding spaces matches them, and they are part of the
        // span in the document. Trimming them is a small thing to get wrong on the page whose claim
        // is that it reproduces what the document said.
        var findings = new[] { Signal("lex.x", 3.0, 0, " TARGET ") };

        var html = EvidenceReport.ToHtml(Synthetic(findings));

        Assert.Contains("“ TARGET ”", html);
    }

    [Fact]
    public void A_backslash_in_the_document_cannot_defeat_the_escape()
    {
        // Escaping only the bracket is escaping that one extra character defeats: a document already
        // containing \<script> becomes \\<script>, which Markdown reads as an escaped backslash and
        // then a live tag. The backslash has to be escaped first or the rest is theatre.
        var findings = new[] { Signal("lex.x", 3.0, 0, @"\<script>alert(1)</script>") };

        var markdown = EvidenceReport.ToMarkdown(Synthetic(findings));

        Assert.DoesNotContain("<script", markdown.Replace(@"\\", "").Replace(@"\<", ""));

        // And the document's own backslash is still on the page: it was protected, not deleted.
        Assert.Contains(@"\\\<script>", markdown);

        // The HTML resolves both escapes and shows exactly what the document said, once.
        var html = EvidenceReport.ToHtml(Synthetic(findings));
        Assert.Contains(@"\&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void The_html_page_still_reproduces_the_matched_text_exactly()
    {
        // The escaping must survive one pass and exactly one. Reproducing the matched text as the
        // document wrote it is the claim this product rests on, and a page showing "\<img" or
        // "&amp;lt;img" has corrupted the evidence in the course of protecting it.
        var findings = new[] { Signal("lex.x", 3.0, 0, "<img src=x>") };

        var html = EvidenceReport.ToHtml(Synthetic(findings));

        Assert.Contains("&lt;img src=x&gt;", html);
        Assert.DoesNotContain("\\&lt;", html);
        Assert.DoesNotContain("&amp;lt;", html);
    }

    [Fact]
    public void A_pipe_in_a_list_item_is_shown_as_a_pipe()
    {
        // Table cells lost their backslash in SplitRow, which has to resolve it before it can tell a
        // column boundary from a pipe inside a filename. List items had no such step, so the escape
        // that protected the table leaked onto the page everywhere else.
        var findings = new[] { Signal("lex.x", 3.0, 0, "either|or") };

        var html = EvidenceReport.ToHtml(Synthetic(findings));

        Assert.Contains("either|or", html);
        Assert.DoesNotContain("either\\|or", html);
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

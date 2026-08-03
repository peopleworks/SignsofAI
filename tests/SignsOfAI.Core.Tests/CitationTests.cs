using SignsOfAI.Core.Citations;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The citation cross-check earns its place by being *actionable*: a teacher cannot take "87% AI" to
/// an integrity committee, but "the text cites Martínez (2019) and no Martínez appears in its own
/// bibliography" settles itself with one question.
///
/// That only holds while the claim is true. So, as with the character artifacts, most of what follows
/// is about the cases that must produce nothing — an accented surname cited without its accent, a
/// figure caption that happens to end in a year, a document with no bibliography at all. A wrong
/// "missing reference" is not a debatable reading of someone's prose; it sends them looking for
/// something that is already there, and it only has to happen once.
/// </summary>
public class CitationCheckerTests
{
    private const int Now = 2026;

    private const string Numbered = """
        Transformer models changed how we approach this problem [1]. Later work [2,3] extended it,
        and a survey [1] revisits the whole area.

        ## References

        [1] Vaswani, A., et al. (2017). Attention is all you need. NeurIPS.
        [2] Devlin, J., et al. (2019). BERT: pre-training of deep bidirectional transformers. NAACL.
        [3] Brown, T., et al. (2020). Language models are few-shot learners. NeurIPS.
        """;

    private const string AuthorYear = """
        The effect was first described by Salgado (2018), and later work confirmed it
        (Martínez & Ruiz, 2020). A third study disagreed (Okonkwo, 2021).

        ## References

        Salgado, R. (2018). Measuring what matters. Journal of Assessment, 12(3), 44-61.
        Martínez, L., & Ruiz, P. (2020). A replication study. Educational Review, 8(1), 5-19.
        Okonkwo, A. (2021). Counter-evidence from three cohorts. Learning Science, 4(2), 90-102.
        """;

    // ---- the checks fire when they should ------------------------------------------------------------

    [Fact]
    public void A_consistent_numbered_document_has_nothing_wrong_with_it()
    {
        var report = CitationChecker.Check(Numbered, currentYear: Now);

        Assert.True(report.HasReferenceList);
        Assert.Equal(3, report.References.Count);
        Assert.Equal(CitationStyle.Numbered, report.Style);
        Assert.Equal(0, report.ContradictionCount);
    }

    [Fact]
    public void A_consistent_author_year_document_has_nothing_wrong_with_it()
    {
        var report = CitationChecker.Check(AuthorYear, currentYear: Now);

        Assert.True(report.HasReferenceList);
        Assert.Equal(3, report.References.Count);
        Assert.Equal(CitationStyle.AuthorYear, report.Style);
        Assert.Equal(0, report.ContradictionCount);
    }

    [Fact]
    public void A_number_beyond_the_end_of_the_list_is_a_contradiction()
    {
        var text = Numbered.Replace("Later work [2,3]", "Later work [2,7]");

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.CitedButNotListed);

        Assert.Equal("[7]", issue.Subject);
        Assert.True(issue.IsContradiction);
        Assert.Contains("3", issue.Message, StringComparison.Ordinal); // says how many entries exist
    }

    [Fact]
    public void A_surname_nowhere_in_the_bibliography_is_a_contradiction()
    {
        // The signature of an invented reference: a name in the prose that the document's own list
        // has never heard of.
        var text = AuthorYear.Replace("(Okonkwo, 2021)", "(Fairweather, 2021)");

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.CitedButNotListed);

        Assert.Equal("Fairweather, 2021", issue.Subject);
        Assert.True(issue.IsContradiction);
    }

    [Fact]
    public void The_same_missing_source_cited_five_times_is_one_problem()
    {
        var text = AuthorYear.Replace(
            "A third study disagreed (Okonkwo, 2021).",
            "A third study disagreed (Fairweather, 2021). It was replicated (Fairweather, 2021) " +
            "and reviewed (Fairweather, 2021).");

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.CitedButNotListed);
    }

    [Fact]
    public void A_malformed_doi_cannot_be_a_doi()
    {
        var text = Numbered.Replace("NeurIPS.\n[2]", "NeurIPS. https://doi.org/10.55/incomplete\n[2]");

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.MalformedDoi);

        Assert.Contains("10.55/", issue.Subject, StringComparison.Ordinal);
        Assert.True(issue.IsContradiction);
    }

    [Fact]
    public void One_doi_cannot_name_two_different_works()
    {
        var text = Numbered
            .Replace("NeurIPS.\n[2]", "NeurIPS. https://doi.org/10.5555/abc123\n[2]")
            .Replace("NAACL.", "NAACL. https://doi.org/10.5555/abc123");

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.RepeatedDoi);

        Assert.Equal("10.5555/abc123", issue.Subject);
        Assert.True(issue.IsContradiction);
    }

    [Fact]
    public void A_year_that_has_not_happened_yet_is_a_contradiction()
    {
        var text = AuthorYear.Replace("Okonkwo, A. (2021)", "Okonkwo, A. (2031)");

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.ImpossibleYear);

        Assert.Equal("2031", issue.Subject);
        Assert.True(issue.IsContradiction);
    }

    [Fact]
    public void An_entry_the_text_never_mentions_is_reported_but_is_not_a_contradiction()
    {
        // People legitimately list further reading, and reference managers leave entries behind.
        var text = AuthorYear + "\nKowalski, J. (2016). Something else entirely. Other Journal, 1(1), 1-9.";

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.ListedButNotCited);

        Assert.False(issue.IsContradiction);
        Assert.Equal(0, report.ContradictionCount);
    }

    [Fact]
    public void The_same_reference_listed_twice_is_reported()
    {
        var text = AuthorYear + "\nOkonkwo, A. (2021). Counter-evidence from three cohorts. Learning Science, 4(2), 90-102.";

        var report = CitationChecker.Check(text, currentYear: Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.DuplicateReference);

        Assert.False(issue.IsContradiction);
    }

    // ---- what must never fire --------------------------------------------------------------------------

    [Fact]
    public void An_accent_dropped_from_a_citation_is_not_a_missing_source()
    {
        // "(Martinez, 2020)" against a bibliography spelling it "Martínez". Getting this wrong would
        // single out precisely the writers this project exists to stop singling out.
        var text = AuthorYear.Replace("(Martínez & Ruiz, 2020)", "(Martinez & Ruiz, 2020)");

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.DoesNotContain(report.Issues, i => i.Kind == CitationIssueKind.CitedButNotListed);
    }

    [Fact]
    public void A_caption_that_ends_in_a_year_is_not_a_citation()
    {
        var text = AuthorYear.Replace(
            "A third study disagreed (Okonkwo, 2021).",
            "A third study disagreed (Okonkwo, 2021). The trend is plotted in (Figure 2019) " +
            "and tabulated in (Table 2020), collected in (March 2018).");

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.DoesNotContain(report.Issues, i => i.Kind == CitationIssueKind.CitedButNotListed);
    }

    [Fact]
    public void With_no_reference_list_the_cross_checks_do_not_run_at_all()
    {
        // "Cited but not listed" is meaningless without a list, and guessing where a bibliography
        // starts would manufacture accusations out of formatting.
        const string text = "The effect was described by Salgado (2018) and confirmed later (Ruiz, 2020).";

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.False(report.HasReferenceList);
        Assert.Empty(report.Issues);
        Assert.Equal(2, report.Citations.Count);
        Assert.Contains("cross-checks", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prose_under_the_heading_is_not_counted_as_a_reference()
    {
        var text = Numbered.Replace("## References\n", "## References\n\nAll sources were consulted online.\n");

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.Equal(3, report.References.Count);
    }

    [Fact]
    public void Entries_inside_the_list_are_not_read_as_citations_of_anything()
    {
        // Each entry carries "(2017)" and friends. Counting those as in-text citations would double
        // every document's citation count and invent cross-check failures.
        var report = CitationChecker.Check(AuthorYear, currentYear: Now);

        Assert.All(report.Citations, c => Assert.True(c.Span.Start < report.References[0].Span.Start));
    }

    [Fact]
    public void A_bibliography_wrapped_by_a_PDF_is_not_split_into_fragments()
    {
        // Regression. Extracted PDFs lose the hanging indent, so entries arrive across several lines.
        // Treating "Journal of Educational Measurement, 59(4), 512-538." as a new entry invented a
        // reference nobody wrote and then complained that it was never cited — a false accusation
        // manufactured entirely out of line breaks.
        const string wrapped = """
            Recent work has questioned automated scoring (Hernández-Silva, 2022), though earlier
            findings suggested otherwise (Okoro, 2019).

            References

            Hernández-Silva, M. (2022). Reliability of automated scoring in large cohorts.
                Journal of Educational Measurement, 59(4), 512-538.
                https://doi.org/10.1111/jedm.12345
            Okoro, C. (2019). Inter-rater agreement revisited: a meta-analysis of forty
                studies across three decades. Assessment in Education, 26(2), 145-170.
            """;

        var report = CitationChecker.Check(wrapped, currentYear: Now);

        Assert.Equal(2, report.References.Count);
        Assert.Equal("10.1111/jedm.12345", report.References[0].Doi);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void A_year_hiding_inside_a_DOI_is_not_a_year()
    {
        // Regression, and a nasty one. "10.1080/aie.2022.4471" carries a 2022 that has nothing to do
        // with when the work was published. Reading it as a year split the wrapped entry at the wrong
        // line, and on a DOI like ".../2027." it would have reported an ordinary reference as
        // published in the future — an accusation assembled entirely out of an identifier.
        const string text = """
            Autonomy mediates the effect (Hernández-Silva, 2022).

            References

            Hernández-Silva, M. (2022). Autonomy as a mediator of feedback effects.
                Assessment in Education, 29(4), 512-538. https://doi.org/10.1080/aie.2027.4471
            """;

        var report = CitationChecker.Check(text, currentYear: Now);
        var reference = Assert.Single(report.References);

        Assert.Equal(2022, reference.Year);
        Assert.DoesNotContain(report.Issues, i => i.Kind == CitationIssueKind.ImpossibleYear);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void A_document_with_no_sources_at_all_reports_nothing()
    {
        var report = CitationChecker.Check("The bus was late again. I walked instead.", currentYear: Now);

        Assert.False(report.Any);
        Assert.Equal(CitationStyle.None, report.Style);
    }

    [Fact]
    public void Empty_and_null_input_are_handled()
    {
        Assert.False(CitationChecker.Check(null).Any);
        Assert.False(CitationChecker.Check("   ").Any);
    }

    // ---- shapes it has to understand ---------------------------------------------------------------------

    [Fact]
    public void A_range_cites_every_number_in_it()
    {
        var text = Numbered.Replace("Later work [2,3]", "Later work [1-3]");

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.Equal(0, report.ContradictionCount);
        Assert.Contains(report.Citations, c => c.Number == 2);
    }

    [Fact]
    public void One_pair_of_brackets_can_hold_two_citations()
    {
        var text = AuthorYear.Replace(
            "(Martínez & Ruiz, 2020)", "(Martínez & Ruiz, 2020; Okonkwo, 2021)");

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.Contains(report.Citations, c => c.Surname == "Martínez");
        Assert.Contains(report.Citations, c => c.Surname == "Okonkwo");
        Assert.Equal(0, report.ContradictionCount);
    }

    [Fact]
    public void Spanish_headings_are_recognised()
    {
        const string text = """
            El efecto lo describió primero Salgado (2018), y trabajos posteriores lo confirmaron
            (Martínez y Ruiz, 2020).

            ## Bibliografía

            Salgado, R. (2018). Medir lo que importa. Revista de Evaluación, 12(3), 44-61.
            Martínez, L., y Ruiz, P. (2020). Un estudio de réplica. Reseña Educativa, 8(1), 5-19.
            """;

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.True(report.HasReferenceList);
        Assert.Equal(2, report.References.Count);
        Assert.Equal(0, report.ContradictionCount);
    }

    [Fact]
    public void The_wording_comes_from_the_pack_so_Spanish_reads_in_Spanish()
    {
        var pack = Core.Rules.RulePackLoader.Load("es");
        var text = AuthorYear.Replace("(Okonkwo, 2021)", "(Fairweather, 2021)");

        var report = CitationChecker.Check(text, pack, Now);
        var issue = Assert.Single(report.Issues, i => i.Kind == CitationIssueKind.CitedButNotListed);

        Assert.Contains("no aparece", issue.Message, StringComparison.Ordinal);
        Assert.Contains("Pida la fuente", report.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void An_appendix_after_the_bibliography_is_not_part_of_it()
    {
        var text = Numbered + "\n\n## Appendix A\n\nThe survey instrument used in 2024 is reproduced below.\n";

        var report = CitationChecker.Check(text, currentYear: Now);

        Assert.Equal(3, report.References.Count);
    }
}

/// <summary>The citation report seen through the public entry point, alongside everything else.</summary>
public class CitationIntegrationTests
{
    [Fact]
    public void Citations_never_move_the_score()
    {
        const string clean = """
            We must delve into the rich tapestry of this multifaceted subject, because it is worth
            noting that the approach is nuanced (Salgado, 2018).

            ## References

            Salgado, R. (2018). Measuring what matters. Journal of Assessment, 12(3), 44-61.
            """;
        var broken = clean.Replace("(Salgado, 2018)", "(Fairweather, 2018)");

        var analyzer = new AiWritingAnalyzer();
        var before = analyzer.Analyze(clean, "en");
        var after = analyzer.Analyze(broken, "en");

        Assert.Equal(before.OverallScore, after.OverallScore);
        Assert.Equal(0, before.Citations.ContradictionCount);
        Assert.Equal(1, after.Citations.ContradictionCount);
    }

    [Fact]
    public void A_substituted_letter_cannot_hide_a_citation_from_its_own_bibliography()
    {
        // The reference list spells the name with a Cyrillic "е", so a naive string search would find
        // the surname and report nothing. Analysis runs on the normalized copy, so it does not.
        var cyrillicE = char.ConvertFromUtf32(0x0435);
        var text = $"""
            The effect was first described by Fairweather (2018).

            ## References

            Fairw{cyrillicE}ather, R. (2018). Measuring what matters. Journal of Assessment, 12(3), 44-61.
            """;

        var result = new AiWritingAnalyzer().Analyze(text, "en");

        Assert.Equal(0, result.Citations.ContradictionCount);
        Assert.True(result.Artifacts.Any);
    }

    [Fact]
    public void Ordinary_prose_carries_an_empty_citation_report()
    {
        var result = new AiWritingAnalyzer().Analyze("The bus was late again. I walked instead.", "en");

        Assert.False(result.Citations.Any);
    }
}

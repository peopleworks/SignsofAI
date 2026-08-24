using System.Globalization;
using System.Text;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Reporting;

/// <summary>
/// The analysis as a document somebody can keep, forward, print or attach.
///
/// Until this existed the evidence died when the tab closed. That is a hole in the argument rather
/// than a missing feature: this project's entire claim is that a percentage is not evidence and that
/// it will show you the actual tells, the hidden characters at their line and column, the citations a
/// document makes that its own bibliography contradicts. A teacher who cannot forward any of that to
/// a student, or take it to an integrity committee, has been shown something and handed nothing.
///
/// Three rules govern what comes out, and none of them is cosmetic.
///
/// <para><b>It prints its own error rate.</b> Every report carries the threshold this build
/// recommends, how it was measured, and the upper bound of the interval — not the observed rate. A
/// document that accuses somebody without saying how often the method is wrong is the artefact this
/// project was built to argue against, and producing one with a nicer layout would be worse than
/// producing none.</para>
///
/// <para><b>It separates facts from judgements.</b> The score is an opinion about prose. A character
/// at line 37 column 64, and a citation missing from the reference list beside it, are neither
/// opinions nor percentages: they are either there or they are not. They are printed apart, and they
/// are the part a committee can actually act on.</para>
///
/// <para><b>It contains the text, so it says so.</b> The report is generated on the reader's own
/// device and saved by them. That has to be on the page, because the share card — the other thing
/// this tool exports — is built for posting in public, and confusing the two would publish a
/// student's work.</para>
/// </summary>
public static class EvidenceReport
{
    /// <summary>
    /// The report as Markdown: for pasting into an email, an LMS comment box or an issue. Deliberately
    /// the primary form — it survives being quoted, and a teacher can delete the half they do not need
    /// before sending it, which nobody can do with a PDF.
    /// </summary>
    public static string ToMarkdown(AnalysisResult result, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var text = ReportMessages.For(o.InterfaceLanguage);
        var sb = new StringBuilder();
        var title = string.Equals(o.Title, ReportOptions.Default.Title, StringComparison.Ordinal)
            ? text.Get(ReportMessages.DefaultTitle).Text
            : o.Title;
        // MaxRows is public, settable and unvalidated, and this library is on NuGet. A negative value
        // printed "… and 12 more" for two findings — a count of omitted evidence larger than the
        // evidence — because the arithmetic is Count minus the limit. Clamped once, here, rather than
        // trusted at four call sites.
        var rows = Math.Max(0, o.MaxRows);

        // Through Cell like everything else this report did not write: the title is a caller's string,
        // and every host that has one builds it from a filename.
        sb.Append("# ").Append(Cell(title)).AppendLine();
        sb.AppendLine();
        var fallbackNoticeAt = sb.Length;
        if (!string.IsNullOrWhiteSpace(o.DocumentName))
            AppendBlock(sb, text, ReportMessages.MetaDocument, Cell(o.DocumentName));
        AppendBlock(sb, text, ReportMessages.MetaGenerated, Cell(o.GeneratedOn), Cell(o.EngineVersion));
        sb.AppendLine();

        // ── The reading, and immediately the caveat that makes it usable ──────────────────────────
        AppendHeading(sb, text, 2, ReportMessages.SectionAnalysis);
        sb.AppendLine();
        // The verdict is withheld below the threshold this build can support, because printing
        // "Reads mostly human" and then, four lines down, "treat the score as saying nothing" is a
        // page arguing with itself — and the reader will keep whichever half suits them. A low score
        // is not evidence of a human: a detector that detects nothing also returns zero, and this
        // project deliberately never measured how much machine writing it catches.
        if (VerdictHolds(result))
            AppendBlock(sb, text, ReportMessages.AnalysisScoreWithVerdict,
                Num(result.OverallScore), Verdict(text, result.OverallScore));
        else
            AppendBlock(sb, text, ReportMessages.AnalysisScoreWithoutVerdict,
                Num(result.OverallScore));
        sb.AppendLine();
        if (!VerdictHolds(result))
        {
            // Which of the reasons, because they are not the same statement. "Below the threshold"
            // is a reading — the tool looked and found little. "Shorter than anything measured" is
            // a refusal to read at all, and a reader who is told the first when the second is true
            // will take away a reassurance nobody offered.
            AppendBlock(sb, text,
                VerdictBands.Measured(result.Statistics.WordCount)
                    ? ReportMessages.AnalysisNoVerdict
                    : ReportMessages.AnalysisNoVerdictShort,
                Num(result.Statistics.WordCount), Num(VerdictBands.MinimumWords ?? 0));
            sb.AppendLine();
        }

        // Named up here rather than left to the section below, because a page that opens "0/100" and
        // buries four contradictions in its own bibliography further down has chosen the wrong thing
        // to make salient — and that exact document is the one this project keeps writing about.
        // Contradictions, not issues. CitationIssue draws this line itself — a cited source missing
        // from the list is a contradiction, an uncited entry in the list is not, "because people
        // legitimately list further reading" — and the headline used to count both. A document whose
        // only fault was further reading was announced as "1 source contradiction", and the note
        // below the section then asserted in the report's own voice that it "disagrees with itself".
        // That is an accusation the checker had explicitly declined to make, printed on the page a
        // teacher takes to a committee, and it shipped for months.
        var contradictions = result.Citations.ContradictionCount;
        if (contradictions > 0 || result.Artifacts.Any)
        {
            if (contradictions > 0 && result.Artifacts.Any)
                AppendBlock(sb, text,
                    contradictions == 1
                        ? result.Artifacts.Count == 1
                            ? ReportMessages.AnalysisFactsBothOneOne
                            : ReportMessages.AnalysisFactsBothOneOther
                        : result.Artifacts.Count == 1
                            ? ReportMessages.AnalysisFactsBothOtherOne
                            : ReportMessages.AnalysisFactsBothOtherOther,
                    contradictions, result.Artifacts.Count);
            else if (contradictions > 0)
                AppendBlock(sb, text, contradictions == 1
                        ? ReportMessages.AnalysisFactsCitationOne
                        : ReportMessages.AnalysisFactsCitationOther,
                    contradictions);
            else
                AppendBlock(sb, text, result.Artifacts.Count == 1
                        ? ReportMessages.AnalysisFactsArtifactOne
                        : ReportMessages.AnalysisFactsArtifactOther,
                    result.Artifacts.Count);
            sb.AppendLine();
        }
        if (result.Observations.Count > 0)
            AppendBlock(sb, text, result.Signals.Count == 1
                    ? ReportMessages.AnalysisCountsWithObservationsOne
                    : ReportMessages.AnalysisCountsWithObservationsOther,
                result.Signals.Count, result.Observations.Count);
        else
            AppendBlock(sb, text, result.Signals.Count == 1
                    ? ReportMessages.AnalysisCountsOne
                    : ReportMessages.AnalysisCountsOther,
                result.Signals.Count);
        AppendBlock(sb, text, ReportMessages.AnalysisLanguageStats,
            LanguageName(text, result.Language), result.Statistics.WordCount,
            result.Statistics.SentenceCount, Num(result.Statistics.Burstiness, 2));
        sb.AppendLine();

        // Said before the error rate, because it outranks it: a rate measured on English writing
        // describes nothing about what these rules do to French prose they were never written for.
        if (result.RulePackLanguage is { Length: > 0 } packLanguage
            && !packLanguage.Equals(result.Language, StringComparison.OrdinalIgnoreCase))
        {
            AppendBlock(sb, text, ReportMessages.NoRulePack, LanguageName(text, result.Language));
            sb.AppendLine();
        }

        AppendLocalized(sb, text, Caveat(text, result.Language));
        sb.AppendLine();

        // ── Checkable facts first, because they are the part that settles anything ────────────────
        if (result.Artifacts.Any || result.Citations.Any)
        {
            AppendHeading(sb, text, 2, ReportMessages.SectionCheckable);
            sb.AppendLine();
            AppendBlock(sb, text, ReportMessages.CheckableIntro);
            sb.AppendLine();

            if (result.Artifacts.Any)
            {
                AppendHeading(sb, text, 3, ReportMessages.SectionCharacters);
                sb.AppendLine();
                sb.AppendLine(Cell(result.Artifacts.Summary));
                sb.AppendLine();
                // The heading used to say "characters writing does not produce", which is false for
                // half of what this table lists: Word makes soft hyphens on its own and a stray
                // non-breaking space arrives with any copy-paste from a web page. Only the invisible
                // and impostor-letter kinds are hard to arrive at innocently, and even they can be
                // pasted in. Saying so here is the difference between a fact and an insinuation.
                AppendBlock(sb, text, ReportMessages.CharactersExplanation);
                sb.AppendLine();
                // Said above the one table the reader is invited to check in an editor. Without it
                // the Line column reads 6, 1, 2, 3 and looks like the tool lost its place — the
                // same reason the signals list carries its own ordering line.
                AppendBlock(sb, text, ReportMessages.CharactersOrdered);
                sb.AppendLine();
                AppendBlock(sb, text, ReportMessages.CharactersTableHeader);
                sb.AppendLine("|---|---|---:|---:|");
                // Strong kinds first, then by position. A file can hold two hundred soft hyphens —
                // Word inserts them unprompted — and one letter borrowed from another alphabet. In
                // document order the innocent two hundred fill the table and the one occurrence that
                // is hard to arrive at by accident falls off the end. IsStrong is the scanner's own
                // published distinction, not a new judgement invented for the page.
                foreach (var occurrence in result.Artifacts.Occurrences
                             .OrderByDescending(a => a.IsStrong)
                             .ThenBy(a => a.Line).ThenBy(a => a.Column)
                             .Take(rows))
                    sb.Append("| ").Append(Cell(Describe(occurrence.Kind))).Append(" | `")
                      .Append(occurrence.CodePoint).Append("` | ").Append(occurrence.Line)
                      .Append(" | ").Append(occurrence.Column).AppendLine(" |");
                if (result.Artifacts.Occurrences.Count > rows)
                {
                    sb.AppendLine();
                    AppendBlock(sb, text, ReportMessages.MoreRows,
                        result.Artifacts.Occurrences.Count - rows);
                }
                sb.AppendLine();
            }

            if (result.Citations.Any)
            {
                AppendHeading(sb, text, 3, ReportMessages.SectionCitations);
                sb.AppendLine();
                sb.AppendLine(Cell(result.Citations.Summary));
                sb.AppendLine();
                // Contradictions before untidiness, then in document order. Cutting this list by
                // position was the same defect as the signals list and it was left in place one
                // commit ago: forty uncited entries early in a long bibliography would print, and
                // the repeated DOI in the last one — the finding that settles anything — would not.
                foreach (var issue in result.Citations.Issues
                             .OrderByDescending(i => i.IsContradiction).ThenBy(i => i.Line)
                             .Take(rows))
                    sb.Append("- ").AppendLine(Cell(issue.Message));
                // This list used to stop at forty in silence, alone among the four. A reader counting
                // the contradictions on the page against the number the summary above states would
                // find the page contradicting itself about a document accused of contradicting itself.
                if (result.Citations.Issues.Count > rows)
                {
                    sb.AppendLine();
                    AppendBlock(sb, text, ReportMessages.MoreRows,
                        result.Citations.Issues.Count - rows);
                }
                sb.AppendLine();
                // Only claimed when something actually contradicts. The first version printed it
                // whenever there was anything to say about sources at all — including "no reference
                // list was found, so the cross-checks were not run", where the page then asserted, in
                // its own voice, a self-contradiction it had explicitly not looked for. In a document
                // that goes to a committee about a nineteen-year-old, that is the precise harm this
                // project exists to argue against.
                AppendBlock(sb, text, result.Citations.ContradictionCount > 0
                    ? ReportMessages.CitationsIssuesNote
                    : ReportMessages.CitationsNoIssuesNote);
                sb.AppendLine();
            }
        }

        // ── The judgement, clearly labelled as one ───────────────────────────────────────────────
        AppendHeading(sb, text, 2, ReportMessages.SectionSignals);
        sb.AppendLine();
        if (result.Signals.Count == 0)
        {
            AppendBlock(sb, text, ReportMessages.SignalsNone);
        }
        else
        {
            // Strongest first, and the page says so. The analyser returns findings in the order they
            // occur in the text, because that is what highlighting and the rewriter need; printing
            // them that way and then cutting at forty meant a long document could spend the whole
            // list on weak hits in its opening pages while the finding that did most to produce the
            // headline number sat in the last paragraph, omitted. The reader was then given a score
            // the visible evidence could not account for — in the one document this project builds
            // for somebody to take into a room where a decision is made about a person.
            //
            // The line says weight and not "how much it moved the score", which was the first
            // wording and is false for one finding. Every non-statistical finding pools into a
            // single weighted density that Map turns into the pattern score, and Map is monotone,
            // so among those the order by weight *is* the order by contribution. The burstiness
            // finding is not one of those: Scorer excludes the Statistical category from that sum
            // and derives its share from the sentence-length distribution instead, so the weight it
            // carries ranks it on this page without being what moved the number. It sorted to the
            // top of the first report generated from this code, which is where the claim would have
            // been read and where it would have been wrong.
            //
            // Ties are broken by how many
            // times that rule has already appeared, and only then by position: sixteen English rules
            // share the weight 3.5, and in a tie the reader is better served by one occurrence of
            // each before any second occurrence than by one rule's run. It reorders strictly within
            // equal weight, so it costs the guarantee nothing — everything omitted still weighs no
            // more than everything shown, which is what the line below is entitled to say.
            //
            // A rule that genuinely outweighs the rest still fills the list with its own repeats,
            // and that is the honest picture: fourteen occurrences of one word are where such a
            // document's number actually comes from. Whether the section should collapse them into
            // a count, as the observations section does, is a question about its shape rather than
            // about which evidence it drops.
            AppendBlock(sb, text, ReportMessages.SignalsOrdered);
            sb.AppendLine();
            foreach (var f in result.Signals
                         .GroupBy(f => f.RuleId)
                         .SelectMany(g => g.OrderBy(f => f.Span.Start)
                                           .Select((f, rank) => (Finding: f, Rank: rank)))
                         .OrderByDescending(x => x.Finding.Weight)
                         .ThenBy(x => x.Rank)
                         .ThenBy(x => x.Finding.Span.Start)
                         .Select(x => x.Finding)
                         .Take(rows))
            {
                sb.Append("- **").Append(f.Category).Append("** — ");
                if (!string.IsNullOrWhiteSpace(f.MatchedText))
                    sb.Append('“').Append(Cell(f.MatchedText)).Append("” — ");
                sb.Append(Cell(f.Message)).Append(' ').Append("*→ ").Append(Cell(f.Suggestion)).AppendLine("*");
            }
            if (result.Signals.Count > rows)
            {
                sb.AppendLine();
                // Not the generic "… and N more": what was cut is now a property of the evidence
                // rather than of where it happened to fall, and the reader is entitled to know that
                // nothing stronger than what they are looking at was left out. That clause needs
                // something to be looking at — at zero rows there is nothing above for the omitted
                // findings to weigh less than, so the generic line, which stays true, is used.
                AppendBlock(sb, text, rows > 0 ? ReportMessages.SignalsMore : ReportMessages.MoreRows,
                    result.Signals.Count - rows);
            }
        }
        sb.AppendLine();

        if (result.Observations.Count > 0)
        {
            AppendHeading(sb, text, 2, ReportMessages.SectionObservations);
            sb.AppendLine();
            AppendBlock(sb, text, ReportMessages.ObservationsIntro);
            sb.AppendLine();
            // Most frequent first, for the same reason as the signals list: a rule used thirty times
            // is the one the reader wants to see, and in rule-id order it can be pushed out by
            // twenty rules that fired once. The id comes from a rule pack, which is JSON anyone can
            // contribute, so it goes through Cell like any other text this report did not write.
            var groups = result.Observations.GroupBy(f => f.RuleId)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .ToList();
            foreach (var group in groups.Take(rows))
                AppendBlock(sb, text, group.Count() == 1
                        ? ReportMessages.ObservationsRowOne
                        : ReportMessages.ObservationsRowOther,
                    Cell(group.Key), group.Count());
            if (groups.Count > rows)
            {
                sb.AppendLine();
                AppendBlock(sb, text, ReportMessages.MoreRows, groups.Count - rows);
            }
            sb.AppendLine();
        }

        AppendHeading(sb, text, 2, ReportMessages.SectionErrorRate);
        sb.AppendLine();
        HowOftenWrong(sb, text, result.Language);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        AppendBlock(sb, text, ReportMessages.PrivacyDocument);

        InsertFallbackSummary(sb, fallbackNoticeAt, text);

        return sb.ToString();
    }

    /// <summary>
    /// The same report as a self-contained HTML page — no stylesheet, no script, no external request —
    /// so it opens anywhere by double-click and prints to PDF from the browser. Chosen over generating
    /// a PDF directly because a PDF writer is a dependency, and this library has none, which is what
    /// lets it run inside a browser tab.
    /// </summary>
    public static string ToHtml(AnalysisResult result, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var text = ReportMessages.For(o.InterfaceLanguage);
        var title = string.Equals(o.Title, ReportOptions.Default.Title, StringComparison.Ordinal)
            ? text.Get(ReportMessages.DefaultTitle).Text
            : o.Title;
        var body = MarkdownToHtml(ToMarkdown(result, o));

        return $"""
            <!doctype html>
            <html lang="{text.Language}">
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Escape(title)}</title>
            <style>
            {Css}
            </style>
            {body}
            </html>
            """;
    }

    /// <summary>
    /// A whole folder as one document: every file scanned, ordered worst first, over the same caveat
    /// every single-document report carries.
    ///
    /// A stack of essays is what a teacher actually has, and the honest thing a triage list can do is
    /// tell them where to <em>look</em> — not which student to accuse. So the top of the list is the
    /// reading order, and the page says so, because a table sorted by score with no explanation reads
    /// like a ranking of guilt.
    /// </summary>
    public static string FolderToMarkdown(
        string folderName, IReadOnlyList<FolderEntry> entries, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var text = ReportMessages.For(o.InterfaceLanguage);
        var sb = new StringBuilder();
        var title = string.Equals(o.Title, ReportOptions.Default.Title, StringComparison.Ordinal)
            ? text.Get(ReportMessages.DefaultTitle).Text
            : o.Title;
        // An error wins over a score: a file that failed to read has no business in the reading order,
        // and nothing on the public FolderEntry stops a caller supplying both.
        var unreadable = entries.Where(e => e.Error is not null).ToList();
        var scored = entries.Where(e => e.Error is null && e.Score is not null).ToList();

        sb.Append("# ").AppendLine(Cell(title));
        sb.AppendLine();
        var fallbackNoticeAt = sb.Length;
        AppendBlock(sb, text, ReportMessages.MetaFolder, Cell(folderName));
        AppendBlock(sb, text, ReportMessages.MetaGenerated, Cell(o.GeneratedOn), Cell(o.EngineVersion));
        sb.AppendLine();
        AppendBlock(sb, text, unreadable.Count > 0
                ? entries.Count == 1
                    ? ReportMessages.FolderSummaryUnreadableOne
                    : ReportMessages.FolderSummaryUnreadableOther
                : entries.Count == 1
                    ? ReportMessages.FolderSummaryOne
                    : ReportMessages.FolderSummaryOther,
            unreadable.Count > 0 ? [entries.Count, unreadable.Count] : [entries.Count]);
        sb.AppendLine();
        AppendLocalized(sb, text, Caveat(text, null));
        sb.AppendLine();
        AppendBlock(sb, text, ReportMessages.FolderReadingOrder);
        sb.AppendLine();

        AppendBlock(sb, text, ReportMessages.FolderTableHeader);
        sb.AppendLine("|---|---:|---:|---:|");
        // Every file, deliberately unlike the findings lists. This is the document a teacher keeps
        // after closing the app, and a scan of two hundred essays that silently omitted a hundred and
        // sixty of them — including every low scorer, which is the result that settles a suspicion —
        // would be worse than no document.
        foreach (var e in scored.OrderByDescending(e => e.Score).ThenBy(e => e.Name))
            sb.Append("| ").Append(Cell(e.Name)).Append(" | ").Append(Num(e.Score ?? 0, 0))
              .Append(" | ").Append(e.Signals?.ToString(CultureInfo.InvariantCulture) ?? "—")
              .Append(" | ").Append(e.Words?.ToString(CultureInfo.InvariantCulture) ?? "—")
              .AppendLine(" |");
        sb.AppendLine();

        if (unreadable.Count > 0)
        {
            AppendHeading(sb, text, 2, ReportMessages.SectionUnreadable);
            sb.AppendLine();
            foreach (var e in unreadable)
                AppendBlock(sb, text, ReportMessages.FolderUnreadableRow,
                    Cell(e.Name), Cell(e.Error));
            sb.AppendLine();
        }

        AppendHeading(sb, text, 2, ReportMessages.SectionErrorRate);
        sb.AppendLine();
        HowOftenWrong(sb, text, null);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        AppendBlock(sb, text, ReportMessages.PrivacyFolder);

        InsertFallbackSummary(sb, fallbackNoticeAt, text);

        return sb.ToString();
    }

    /// <inheritdoc cref="FolderToMarkdown"/>
    public static string FolderToHtml(
        string folderName, IReadOnlyList<FolderEntry> entries, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var text = ReportMessages.For(o.InterfaceLanguage);
        var title = string.Equals(o.Title, ReportOptions.Default.Title, StringComparison.Ordinal)
            ? text.Get(ReportMessages.DefaultTitle).Text
            : o.Title;
        return $"""
            <!doctype html>
            <html lang="{text.Language}">
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Escape(title)}</title>
            <style>
            {Css}
            </style>
            {MarkdownToHtml(FolderToMarkdown(folderName, entries, o))}
            </html>
            """;
    }

    /// <summary>
    /// Whether the score has earned the right to carry a verdict.
    ///
    /// This asked for the threshold measured for the language actually analysed, refusing to borrow
    /// the aggregate. That reading was too strict by a wide margin, and the cost was not theoretical:
    /// no language in the corpus supports its own threshold — English has 65 texts and Spanish 25,
    /// against the ~75 the interval needs — so the condition was false for every document in every
    /// language, and the exported report never carried a verdict at all. A tool that renders nothing
    /// has not been careful, it has been switched off, and Spanish would have stayed switched off
    /// for years while the page still promised a reading.
    ///
    /// The distinction that resolves it: borrowing the aggregate *error rate* would misstate how
    /// often this build is wrong about Spanish (13.3% measured, against 5.6% for English and 4.1%
    /// pooled), and that remains forbidden — the caveat below still quotes the language's own bound
    /// and never the pooled one. Borrowing the aggregate *boundary* states nothing about reliability;
    /// it decides when the tool speaks, and it is published, measured and printed on the page beside
    /// the language's own figure. See issue #32.
    /// </summary>
    /// <remarks>
    /// Length joined score and language in #59, and it arrives through <see cref="AnalysisResult"/>
    /// rather than being asked here, so that the CLI's JSON, the MCP payload, the interface and this
    /// page cannot drift into disagreeing about the same document.
    /// </remarks>
    private static bool VerdictHolds(AnalysisResult result) => result.HasVerdict;

    /// <summary>
    /// The sentence that has to appear on every report. Written from the embedded calibration so it
    /// cannot go stale, and explicit when there is none: a fork that has not measured itself says so
    /// rather than inheriting a number it did not earn.
    /// </summary>
    private static LocalizedReportText Caveat(ReportText text, string? language)
    {
        var c = PublishedCalibration.Current;

        if (c is null)
            return text.Get(ReportMessages.CaveatUncalibrated);

        // A single-document report always needs the stratum for the language it actually analysed.
        // Falling through to the aggregate here would attach a measurement from other languages to
        // writing the corpus never contained.
        if (!string.IsNullOrWhiteSpace(language))
        {
            var group = c.For(language);
            if (group is null)
                return text.Get(ReportMessages.CaveatLanguageUnmeasured,
                    LanguageName(text, language));

            if (group.RecommendedThreshold is not { } languageThreshold)
                return text.Get(ReportMessages.CaveatLanguageNoThreshold,
                    group.Texts, Pct(group.BestBound));

            return text.Get(ReportMessages.CaveatLanguageMeasured,
                group.Texts, Num(languageThreshold), Pct(group.BestBound));
        }

        // Measured, but on too little text to support any threshold. Saying "not calibrated" here
        // would contradict the section further down, which goes on to name the corpus and the date.
        if (c.RecommendedThreshold is not { } threshold)
            return text.Get(ReportMessages.CaveatAggregateNoThreshold, c.Texts);

        // "at most" was a guarantee, and a 95% interval does not give one. The corpus is also one
        // genre of writing, so generalising from it to "human writing" is the caller's inference and
        // not this sentence's claim.
        return text.Get(ReportMessages.CaveatAggregateMeasured,
            c.Texts, Num(threshold), Pct(c.RateHigh));
    }

    private static void HowOftenWrong(StringBuilder sb, ReportText text, string? language)
    {
        var c = PublishedCalibration.Current;
        if (c is null)
        {
            AppendBlock(sb, text, ReportMessages.HowUncalibrated);
            return;
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            var group = c.For(language);
            if (group is null)
            {
                AppendBlock(sb, text, ReportMessages.HowLanguageUnmeasured,
                    LanguageName(text, language));
                sb.AppendLine();
                AppendBlock(sb, text, ReportMessages.HowLimitation);
                return;
            }

            if (group.RecommendedThreshold is { } languageThreshold)
                AppendBlock(sb, text, ReportMessages.HowLanguageMeasured,
                    group.Texts, Num(languageThreshold), Pct(group.BestBound), c.MeasuredOn, c.Engine);
            else
                AppendBlock(sb, text, ReportMessages.HowLanguageNoThreshold,
                    group.Texts, Pct(group.BestBound), c.MeasuredOn, c.Engine);
        }
        else
        {
            AppendBlock(sb, text, ReportMessages.HowAggregateIntro,
                c.Texts, c.MeasuredOn, c.Engine);

            if (c.RecommendedThreshold is { } threshold)
            {
                sb.AppendLine();
                AppendBlock(sb, text, ReportMessages.HowAggregateThreshold,
                    Num(threshold), c.FlaggedAtThreshold, c.Texts,
                    Pct((double)c.FlaggedAtThreshold / Math.Max(c.Texts, 1)),
                    Pct(c.RateLow), Pct(c.RateHigh));
                sb.AppendLine();
                AppendBlock(sb, text, ReportMessages.HowReadInterval,
                    c.FlaggedAtThreshold, c.Texts);
            }
        }

        // The noisiest-rule rates are aggregate measurements. They remain useful for a language that
        // is represented in the corpus, but are withheld entirely for an unmeasured language above.
        if (c.NoisiestRules.Count > 0)
        {
            sb.AppendLine();
            AppendBlock(sb, text, ReportMessages.HowNoisyIntro);
            sb.AppendLine();
            foreach (var rule in c.NoisiestRules)
                AppendBlock(sb, text, ReportMessages.HowNoisyRule,
                    rule.RuleId, Pct(rule.TextShare));
        }

        sb.AppendLine();
        AppendBlock(sb, text, ReportMessages.HowLimitation);
    }

    private static string Verdict(ReportText text, double score) => text.Get(score switch
    {
        _ when VerdictBands.Holds(score) => ReportMessages.VerdictSigns,
        _ => ReportMessages.VerdictNone,
    }).Text;

    /// <summary>
    /// The two known names, or the code itself. Through <see cref="Cell"/>, because for anything
    /// other than en and es this prints a string the report did not write: <c>Analyze</c> takes the
    /// language from its caller, the MCP server takes it as free text from a model, and
    /// <see cref="AnalysisResult"/> is a public record any host can construct.
    /// </summary>
    private static string LanguageName(ReportText text, string language) => text.Get(
        language.Equals("en", StringComparison.OrdinalIgnoreCase) ? ReportMessages.LanguageEnglish
        : language.Equals("es", StringComparison.OrdinalIgnoreCase) ? ReportMessages.LanguageSpanish
        : ReportMessages.LanguageOther,
        language.Equals("en", StringComparison.OrdinalIgnoreCase)
            || language.Equals("es", StringComparison.OrdinalIgnoreCase) ? [] : [Cell(language)]).Text;

    private static void AppendBlock(
        StringBuilder sb, ReportText text, string key, params object?[] args) =>
        AppendLocalized(sb, text, text.Get(key, args));

    private static void AppendHeading(StringBuilder sb, ReportText text, int level, string key)
    {
        var value = text.Get(key);
        if (value.FellBack)
        {
            sb.Append('*').Append(text.FallbackMarker).AppendLine("*");
            sb.AppendLine();
        }

        sb.Append('#', level).Append(' ').AppendLine(value.Text);
    }

    private static void AppendLocalized(StringBuilder sb, ReportText text, LocalizedReportText value)
    {
        if (value.FellBack)
        {
            sb.Append('*').Append(text.FallbackMarker).AppendLine("*");
            sb.AppendLine();
        }

        sb.AppendLine(value.Text);
    }

    private static void InsertFallbackSummary(StringBuilder sb, int at, ReportText text)
    {
        // A language with no resource at all is one notice, not sixty markers: every block fell back,
        // so marking each of them would bury the page in its own apology.
        if (text.UnavailableLanguage is { } missing)
        {
            var notice = text.Get(ReportMessages.FallbackLanguage, LanguageName(text, missing)).Text;
            sb.Insert(at, $"*{notice}*{Environment.NewLine}{Environment.NewLine}");
            return;
        }

        if (text.FallbackBlocks == 0) return;
        sb.Insert(at, $"*{text.FallbackSummary}*{Environment.NewLine}{Environment.NewLine}");
    }


    /// <summary>
    /// Inlined rather than linked: the page has to open from a downloads folder years later, on a
    /// machine that has never heard of this project, with no network. Serif on purpose — this is a
    /// document to be read and printed, not an interface.
    /// </summary>
    private const string Css = """
        :root { color-scheme: light dark; }
        body { font: 16px/1.65 ui-serif, Georgia, "Times New Roman", serif;
               max-width: 46rem; margin: 3rem auto; padding: 0 1.2rem; }
        h1 { font-size: 1.7rem; line-height: 1.25; }
        h2 { font-size: 1.15rem; margin-top: 2.2rem; border-bottom: 1px solid #8884;
             padding-bottom: .3rem; }
        h3 { font-size: 1rem; margin-top: 1.6rem; }
        table { border-collapse: collapse; width: 100%; font-size: .92rem; }
        th, td { text-align: left; padding: .35rem .6rem; border-bottom: 1px solid #8883; }
        td:nth-child(n+3), th:nth-child(n+3) { text-align: right; }
        blockquote { margin: 1rem 0; padding: .1rem 1rem; border-left: 3px solid #8886; opacity: .85; }
        code { font-family: ui-monospace, Consolas, monospace; font-size: .9em; }
        hr { border: 0; border-top: 1px solid #8884; margin: 2rem 0; }
        @media print { body { margin: 0; max-width: none; } }
        """;

    private static string Describe(ArtifactKind kind) => kind switch
    {
        ArtifactKind.InvisibleCharacter => "Invisible character",
        ArtifactKind.BidiControl => "Text-direction control",
        ArtifactKind.LookalikeLetter => "Letter from another alphabet",
        ArtifactKind.UnusualSpace => "Unusual space",
        ArtifactKind.SoftHyphen => "Soft hyphen",
        ArtifactKind.VariationSelector => "Variation selector",
        ArtifactKind.PrivateUse => "Private-use character",
        ArtifactKind.TagCharacter => "Tag character (invisible)",
        _ => kind.ToString(),
    };

    private static string Num(double value, int decimals = 1) =>
        Math.Round(value, decimals).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Pct(double fraction) =>
        (fraction * 100).ToString(fraction is > 0 and < 0.1 ? "0.0" : "0.#",
                                  CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// User content on its way into a Markdown line. Three things it must survive being given: a pipe,
    /// which would open an extra table cell and shift every number one column to the right in a table
    /// a teacher reads scores from; a newline, which would end the list item and let whatever
    /// followed become report prose — a line beginning "## " arrived as a heading, in the report's own
    /// voice, from a filename or an extractor's error message; and a <c>&lt;</c>.
    ///
    /// Then two of Markdown's own: <c>[</c>, because escaping the angle bracket blocks raw HTML and
    /// does nothing whatever to <c>![alt](url)</c> — the page's last line says nothing here was
    /// uploaded anywhere, and an image node fetches from somebody else's host the moment a teacher
    /// pastes the report into an LMS, carrying the moment they opened it. Escaping the opening
    /// bracket is enough for both images and links; the <c>!</c> in front of one is only a
    /// character. And <c>*</c>, because the signal row is written as the matched text followed by
    /// the report's own <c>*→ suggestion*</c>: a single asterisk in the document paired with the
    /// report's marker, so emphasis opened at the document's character and closed at the report's,
    /// swallowing the report's explanation into the document's content and deleting the matched
    /// character on the way. That covers <c>**</c> with it, which could shift the pairing of the
    /// category's own bold.
    ///
    /// <c>_</c> and <c>`</c> are deliberately left alone. Neither can pair with a marker this report
    /// writes on the same line, so the worst they do is italicise a document's own text, and
    /// escaping them would put a backslash in front of the underscore in every filename a teacher
    /// reads. A bare URL still autolinks in some renderers; that is a link, not a request, and it
    /// cannot be escaped without mangling the URL the reader needs to see.
    ///
    /// Nothing here is trimmed. Whitespace a rule matched is part of the span in the document, and
    /// this is the page that claims to reproduce what the document said.
    ///
    /// The angle bracket is why the Markdown form needs escaping at all. <see cref="ToHtml"/> escapes on
    /// its way out, so the HTML was never at risk — but Markdown is the form this file documents for
    /// pasting into an LMS comment box or a GitHub issue, and both of those render raw HTML embedded
    /// in Markdown. A document containing <c>&lt;img src=x onerror=…&gt;</c> reached them intact, and
    /// the person pasting it is a teacher who has been told the report is the safe thing to forward.
    /// Backslash rather than an entity, so the character survives one escaping and exactly one:
    /// <see cref="Inline"/> undoes it before escaping for HTML, and the reader sees what the document
    /// actually said. Reproducing the matched text exactly is the claim this product rests on.
    ///
    /// Matched text is user content by definition, and so is anything a community rule pack matches
    /// or names, which is JSON anybody can contribute.
    /// </summary>
    private static string Cell(string? text) =>
        string.IsNullOrEmpty(text)
            ? ""
            // The backslash goes first, and it is not decoration. Escaping only the bracket turns a
            // document that already contains \< into \\< , which Markdown reads as an escaped
            // backslash followed by a live bracket — the escape defeated with one extra character.
            : text.ReplaceLineEndings(" ")
                  .Replace("\\", "\\\\")
                  .Replace("|", "\\|")
                  .Replace("<", "\\<")
                  .Replace("[", "\\[")
                  .Replace("*", "\\*");

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Converts exactly the Markdown this file emits and nothing else. Deliberately not a general
    /// converter: the input is produced twenty lines up, so anything it cannot handle is a bug here
    /// rather than a user's document, and a full parser would be a dependency this library refuses.
    /// </summary>
    private static string MarkdownToHtml(string markdown)
    {
        var html = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        bool inTable = false, inList = false;

        void CloseBlocks()
        {
            if (inTable) { html.AppendLine("</tbody></table>"); inTable = false; }
            if (inList) { html.AppendLine("</ul>"); inList = false; }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("| ", StringComparison.Ordinal))
            {
                // Split on unescaped pipes only. Cell() writes a literal pipe from user content as
                // \| precisely so a filename containing one cannot open an extra column and shift
                // every number in the row one place to the right.
                var cells = SplitRow(line);
                if (!inTable)
                {
                    // The row after the header is the alignment row; it carries no content.
                    if (i + 1 < lines.Length && lines[i + 1].StartsWith("|---", StringComparison.Ordinal))
                    {
                        CloseBlocks();
                        html.Append("<table><thead><tr>");
                        foreach (var cell in cells) html.Append("<th>").Append(cell).Append("</th>");
                        html.AppendLine("</tr></thead><tbody>");
                        inTable = true;
                        i++;
                        continue;
                    }
                }
                else
                {
                    html.Append("<tr>");
                    foreach (var cell in cells) html.Append("<td>").Append(cell).Append("</td>");
                    html.AppendLine("</tr>");
                    continue;
                }
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (inTable) { html.AppendLine("</tbody></table>"); inTable = false; }
                if (!inList) { html.AppendLine("<ul>"); inList = true; }
                html.Append("<li>").Append(Inline(line[2..])).AppendLine("</li>");
                continue;
            }

            CloseBlocks();

            if (line.StartsWith("### ", StringComparison.Ordinal))
                html.Append("<h3>").Append(Inline(line[4..])).AppendLine("</h3>");
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                html.Append("<h2>").Append(Inline(line[3..])).AppendLine("</h2>");
            else if (line.StartsWith("# ", StringComparison.Ordinal))
                html.Append("<h1>").Append(Inline(line[2..])).AppendLine("</h1>");
            else if (line.StartsWith("> ", StringComparison.Ordinal))
                html.Append("<blockquote>").Append(Inline(line[2..])).AppendLine("</blockquote>");
            else if (line.StartsWith("---", StringComparison.Ordinal))
                html.AppendLine("<hr>");
            else if (line.Length > 0)
                html.Append("<p>").Append(Inline(line)).AppendLine("</p>");
        }

        CloseBlocks();
        return html.ToString();
    }


    /// <summary>Table cells of one Markdown row, honouring <c>\|</c> as a literal pipe.</summary>
    private static List<string> SplitRow(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\' && i + 1 < line.Length && line[i + 1] == '|') { cell.Append('|'); i++; }
            else if (line[i] == '|') { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(line[i]);
        }
        cells.Add(cell.ToString());

        // A row is "| a | b |": the split leaves an empty cell at each end.
        if (cells.Count > 0 && cells[0].Trim().Length == 0) cells.RemoveAt(0);
        if (cells.Count > 0 && cells[^1].Trim().Length == 0) cells.RemoveAt(cells.Count - 1);

        return [.. cells.Select(c => Inline(c.Trim()))];
    }

    /// <summary>Bold, italic and code, applied after escaping so a document cannot inject markup.</summary>
    private static string Inline(string text)
    {
        var s = EscapeInline(text);
        s = Wrap(s, "**", "<strong>", "</strong>");
        s = Wrap(s, "`", "<code>", "</code>");
        s = Wrap(s, "*", "<em>", "</em>");
        return s;
    }

    /// <summary>
    /// HTML-escapes a line and resolves the escapes <see cref="Cell"/> wrote, in one pass, so the
    /// page shows the character rather than the backslash that protected it in the Markdown — and so
    /// that a character the document supplied can never be read as a marker by <see cref="Wrap"/>.
    ///
    /// The order is the whole point. Resolving the escapes first and escaping afterwards puts a live
    /// asterisk back into the line before the emphasis pass runs, which is the defect this is here to
    /// stop: the row is written as the matched text and then the report's own <c>*→ suggestion*</c>,
    /// so one asterisk from the document paired with the report's marker and swallowed the report's
    /// explanation into the document's content. An asterisk therefore comes back as <c>&amp;#42;</c>,
    /// which is a literal asterisk to every renderer and is not a character <see cref="Wrap"/> can
    /// see. Nothing else needs the treatment: <c>[</c> and <c>|</c> are not markers here.
    ///
    /// Table cells lose their <c>\|</c> earlier, in <see cref="SplitRow"/>, which has to resolve them
    /// before it can tell a column boundary from a pipe inside a filename. List items and paragraphs
    /// had no such step, so a citation message containing a pipe used to reach the page as <c>\|</c>.
    /// </summary>
    private static string EscapeInline(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length
                && text[i + 1] is '|' or '<' or '[' or '*' or '\\')
            {
                sb.Append(text[++i] switch
                {
                    '*' => "&#42;",
                    '<' => "&lt;",
                    var c => c.ToString(),
                });
                continue;
            }

            sb.Append(text[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                var c => c.ToString(),
            });
        }
        return sb.ToString();
    }

    private static string Wrap(string text, string marker, string open, string close)
    {
        var parts = text.Split(marker);
        if (parts.Length < 3) return text;

        var sb = new StringBuilder(parts[0]);
        for (int i = 1; i < parts.Length; i++)
            sb.Append(i % 2 == 1 ? open : close).Append(parts[i]);

        // An odd number of markers means one is unmatched; leaving a tag open would break the page.
        if (parts.Length % 2 == 0) sb.Append(close);
        return sb.ToString();
    }
}

/// <summary>How a report is labelled. Everything has a default so a caller can pass nothing.</summary>
public sealed record ReportOptions
{
    public string Title { get; init; } = "Writing analysis report";

    /// <summary>
    /// Language of the reader-facing report structure and caveats, independent from the language of
    /// the analysed text. A language without the mandatory report core is rejected rather than
    /// silently producing unreadable caveats; a valid partial translation marks every block that
    /// falls back.
    /// </summary>
    public string InterfaceLanguage { get; init; } = "en";

    /// <summary>The file or assignment this describes. Blank when the text was pasted.</summary>
    public string DocumentName { get; init; } = "";

    public string GeneratedOn { get; init; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    public string EngineVersion { get; init; } =
        typeof(EvidenceReport).Assembly.GetName().Version?.ToString(3) ?? "";

    /// <summary>
    /// Where each list stops. A report meant to be read by a person is worth less at four hundred rows
    /// than at forty, and the count of what was left out is printed rather than the rows themselves.
    ///
    /// Every list that this cuts is sorted strongest first before it is cut, and every one of them
    /// says how many it left out. Truncating a list the analyser returns in document order silently
    /// discards the evidence with the most claim to be on the page — see the signals section. The
    /// folder table is deliberately exempt and prints every file: a scan of two hundred essays that
    /// omitted the low scorers would be withholding the result that settles a suspicion.
    /// </summary>
    public int MaxRows { get; init; } = 40;

    public static ReportOptions Default { get; } = new();
}

/// <summary>One file in a folder scan, as the report needs it.</summary>
public sealed record FolderEntry(
    string Name, int? Words, double? Score, int? Signals, string? Error);

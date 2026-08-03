using System.Globalization;
using System.Text;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Citations;

/// <summary>
/// Checks a document's sources against the document itself.
///
/// This is the second check in the product that returns a fact rather than a judgement, and it exists
/// because of what a teacher can actually do with it. A score saying prose "reads like AI" is
/// arguable by construction and cannot be taken to an integrity committee. "The text cites Martínez
/// (2019) and no Martínez appears in its own bibliography" is not arguable. It is either true of the
/// file or it is not, and the writer can settle it in one sentence.
///
/// Everything here is decided **offline, from the document alone** — no index, no model, no network,
/// nothing sent anywhere. That is not a limitation to apologise for: a bibliography that was invented
/// tends to contradict itself before anyone gets round to asking whether the papers exist, and
/// catching it that way costs the writer no privacy at all. Verifying that a real-looking DOI
/// resolves is a separate, opt-in step, and by then there is a well-formed citation string to send
/// instead of somebody's essay.
///
/// Like the character-artifact report, none of this touches the score.
/// </summary>
public static class CitationChecker
{
    private static readonly RulePack NeutralPack = new() { Language = "*" };

    /// <param name="text">The document.</param>
    /// <param name="pack">Supplies the wording; built-in English when null.</param>
    /// <param name="currentYear">
    /// What "the future" means, injected so the tests do not drift with the calendar. Defaults to now.
    /// </param>
    public static CitationReport Check(string? text, RulePack? pack = null, int? currentYear = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return CitationReport.Empty;

        pack ??= NeutralPack;
        int now = currentYear ?? DateTime.UtcNow.Year;

        var section = CitationExtractor.FindReferenceSection(text);
        var references = section is { } s ? CitationExtractor.ReadEntries(text, s) : [];
        var citations = CitationExtractor.ReadCitations(text, section);

        if (references.Count == 0 && citations.Count == 0)
            return CitationReport.Empty;

        var issues = new List<CitationIssue>();
        string sectionText = section is { } sp ? sp.Slice(text) : string.Empty;
        bool hasList = references.Count > 0;

        if (hasList)
        {
            CheckCitedButNotListed(citations, references, sectionText, text, pack, issues);
            CheckListedButNotCited(references, citations, text, section, pack, issues);
            CheckDois(references, pack, issues);
            CheckDuplicates(references, pack, issues);
        }

        CheckYears(references, citations, now, pack, issues);

        issues.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
        int contradictions = issues.Count(i => i.IsContradiction);

        return new CitationReport
        {
            References = references,
            Citations = citations,
            Issues = issues,
            Style = StyleOf(citations),
            HasReferenceList = hasList,
            ContradictionCount = contradictions,
            Summary = Summarise(references.Count, citations.Count, contradictions, hasList, pack),
            Advice = pack.Text(PackMessages.CitationAdvice),
        };
    }

    // ---- the checks ----------------------------------------------------------------------------------

    /// <summary>
    /// The strongest signal here, and the one a hallucinated bibliography fails first.
    ///
    /// For a numbered style the test is exact: the document cites [12] and has eleven entries. For an
    /// author-year style the test is deliberately blunt — the surname must appear **nowhere** in the
    /// reference section. Matching a citation to its entry properly would mean parsing author lists,
    /// and every parsing mistake becomes a sentence telling someone their reference is missing when it
    /// is sitting right there. "This name is nowhere in your own bibliography" needs no parsing and is
    /// not a thing anyone argues with.
    /// </summary>
    private static void CheckCitedButNotListed(
        List<InTextCitation> citations, List<Reference> references, string sectionText, string text,
        RulePack pack, List<CitationIssue> issues)
    {
        var numbers = references.Where(r => r.Number is not null).Select(r => r.Number!.Value).ToHashSet();
        var folded = Fold(sectionText);
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var citation in citations)
        {
            string subject;
            string message;

            if (citation.Number is { } n)
            {
                // Only meaningful against a list that is actually numbered.
                if (numbers.Count == 0 || numbers.Contains(n)) continue;
                subject = $"[{n}]";
                message = pack.Text(PackMessages.CitationNumberNotListed, n, numbers.Count);
            }
            else if (citation.Surname is { Length: > 1 } surname)
            {
                if (folded.Contains(Fold(surname), StringComparison.Ordinal)) continue;
                subject = citation.Year is { } y ? $"{surname}, {y}" : surname;
                message = pack.Text(PackMessages.CitationNotListed, subject);
            }
            else continue;

            // The same missing source cited ten times is one problem, reported where it first appears.
            if (!reported.Add(subject)) continue;

            issues.Add(new CitationIssue
            {
                Kind = CitationIssueKind.CitedButNotListed,
                Span = citation.Span,
                Line = citation.Line,
                Subject = subject,
                Message = message,
                IsContradiction = true,
            });
        }
    }

    /// <summary>
    /// The mirror image, and deliberately *not* a contradiction: people legitimately list further
    /// reading, and a reference manager will happily leave an entry behind. Reported as something to
    /// look at, never as something wrong.
    /// </summary>
    private static void CheckListedButNotCited(
        List<Reference> references, List<InTextCitation> citations, string text, TextSpan? section,
        RulePack pack, List<CitationIssue> issues)
    {
        var citedNumbers = citations.Where(c => c.Number is not null).Select(c => c.Number!.Value).ToHashSet();
        bool numbered = references.Any(r => r.Number is not null);
        var body = Fold(text[..Math.Min(section?.Start ?? text.Length, text.Length)]);

        foreach (var reference in references)
        {
            string subject;
            if (numbered && reference.Number is { } n)
            {
                if (citedNumbers.Contains(n)) continue;
                subject = $"[{n}]";
            }
            else if (reference.LeadSurname is { Length: > 1 } surname)
            {
                // A second line of defence against a wrapped entry that got split anyway: a real
                // reference carries a year, and complaining about a fragment would be complaining
                // about a line nobody wrote.
                if (reference.Year is null) continue;

                // Searched across the whole body rather than against the parsed citations: a name
                // mentioned in the prose without brackets still counts as used.
                if (body.Contains(Fold(surname), StringComparison.Ordinal)) continue;
                subject = surname;
            }
            else continue;

            issues.Add(new CitationIssue
            {
                Kind = CitationIssueKind.ListedButNotCited,
                Span = reference.Span,
                Line = reference.Line,
                Subject = subject,
                Message = pack.Text(PackMessages.CitationNotCited, Shorten(reference.Raw)),
                IsContradiction = false,
            });
        }
    }

    private static void CheckDois(List<Reference> references, RulePack pack, List<CitationIssue> issues)
    {
        var seen = new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in references)
        {
            foreach (var candidate in CitationExtractor.DoiCandidates(reference.Raw))
            {
                var value = candidate.Value.TrimEnd('.');
                if (!CitationExtractor.IsWellFormedDoi(value))
                {
                    issues.Add(new CitationIssue
                    {
                        Kind = CitationIssueKind.MalformedDoi,
                        Span = reference.Span,
                        Line = reference.Line,
                        Subject = value,
                        Message = pack.Text(PackMessages.CitationMalformedDoi, value),
                        IsContradiction = true,
                    });
                    continue;
                }

                if (!seen.TryGetValue(value, out var list)) seen[value] = list = [];
                list.Add(reference);
            }
        }

        foreach (var (doi, holders) in seen.Where(kv => kv.Value.Count > 1))
        {
            // A DOI names one work. Two entries carrying the same one cannot both be right.
            issues.Add(new CitationIssue
            {
                Kind = CitationIssueKind.RepeatedDoi,
                Span = holders[1].Span,
                Line = holders[1].Line,
                Subject = doi,
                Message = pack.Text(PackMessages.CitationRepeatedDoi, doi, holders.Count),
                IsContradiction = true,
            });
        }
    }

    private static void CheckDuplicates(List<Reference> references, RulePack pack, List<CitationIssue> issues)
    {
        var seen = new Dictionary<string, Reference>(StringComparer.Ordinal);
        foreach (var reference in references)
        {
            var key = Fold(reference.Raw);
            if (seen.TryAdd(key, reference)) continue;

            issues.Add(new CitationIssue
            {
                Kind = CitationIssueKind.DuplicateReference,
                Span = reference.Span,
                Line = reference.Line,
                Subject = Shorten(reference.Raw),
                Message = pack.Text(PackMessages.CitationDuplicate, Shorten(reference.Raw)),
                IsContradiction = false,
            });
        }
    }

    private static void CheckYears(
        List<Reference> references, List<InTextCitation> citations, int now,
        RulePack pack, List<CitationIssue> issues)
    {
        foreach (var reference in references.Where(r => r.Year > now))
        {
            issues.Add(new CitationIssue
            {
                Kind = CitationIssueKind.ImpossibleYear,
                Span = reference.Span,
                Line = reference.Line,
                Subject = reference.Year!.Value.ToString(CultureInfo.InvariantCulture),
                Message = pack.Text(PackMessages.CitationImpossibleYear, reference.Year!.Value, now),
                IsContradiction = true,
            });
        }

        foreach (var citation in citations.Where(c => c.Year > now))
        {
            issues.Add(new CitationIssue
            {
                Kind = CitationIssueKind.ImpossibleYear,
                Span = citation.Span,
                Line = citation.Line,
                Subject = citation.Year!.Value.ToString(CultureInfo.InvariantCulture),
                Message = pack.Text(PackMessages.CitationImpossibleYear, citation.Year!.Value, now),
                IsContradiction = true,
            });
        }
    }

    // ---- presentation ---------------------------------------------------------------------------------

    private static CitationStyle StyleOf(List<InTextCitation> citations)
    {
        bool numbered = citations.Any(c => c.Number is not null);
        bool authorYear = citations.Any(c => c.Surname is not null);
        return (numbered, authorYear) switch
        {
            (true, true) => CitationStyle.Mixed,
            (true, false) => CitationStyle.Numbered,
            (false, true) => CitationStyle.AuthorYear,
            _ => CitationStyle.None,
        };
    }

    private static string Summarise(int references, int citations, int contradictions, bool hasList, RulePack pack) =>
        !hasList
            ? pack.Text(PackMessages.CitationSummaryNoList, citations)
            : contradictions > 0
                ? pack.Text(PackMessages.CitationSummaryIssues, contradictions, references)
                : pack.Text(PackMessages.CitationSummaryClean, references, citations);

    private static string Shorten(string entry) =>
        entry.Length <= 90 ? entry : entry[..87].TrimEnd() + "…";

    /// <summary>
    /// Lower-cased and stripped of accents, so citing "Martinez" against a bibliography that spells it
    /// "Martínez" is not reported as a missing source. Getting this wrong would single out exactly the
    /// writers this project exists to stop singling out.
    /// </summary>
    private static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

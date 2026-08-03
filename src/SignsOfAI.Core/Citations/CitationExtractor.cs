using System.Text.RegularExpressions;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Citations;

/// <summary>
/// Reads a document's reference list and its in-text citations. Parsing only — every judgement about
/// what the two say when compared lives in <see cref="CitationChecker"/>.
///
/// The guiding rule is that **not finding something is always safe and guessing never is**. A
/// bibliography this cannot identify produces no reference list, which switches the cross-checks off
/// entirely; an entry that does not look like a reference is dropped rather than counted. Every
/// mistake in the other direction becomes a sentence telling someone their citation is missing when
/// it is sitting right there, which is the fastest way to make a tool untrustworthy.
/// </summary>
internal static partial class CitationExtractor
{
    // ---- the reference section --------------------------------------------------------------------

    // A heading, alone on its line: markdown hashes, a section number and bold markers are all
    // tolerated because that is what real documents and extracted PDFs look like.
    [GeneratedRegex(
        @"^[ \t]*(?:\#{1,6}[ \t]*)?(?:[IVXLC]+\.|[0-9]+[.)])?[ \t]*(?:\*\*|__)?[ \t]*" +
        @"(references|reference\s+list|bibliography|works\s+cited|literature\s+cited|" +
        @"referencias(?:\s+bibliogr[áa]ficas)?|bibliograf[íi]a|obras\s+citadas|fuentes(?:\s+consultadas)?)" +
        @"[ \t]*(?:\*\*|__)?[ \t]*:?[ \t]*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex ReferenceHeadingRegex();

    // What legitimately follows a bibliography and is not part of it.
    [GeneratedRegex(
        @"^[ \t]*(?:\#{1,6}[ \t]*)?(?:[IVXLC]+\.|[0-9]+[.)])?[ \t]*(?:\*\*|__)?[ \t]*" +
        @"(appendix|appendices|annex|acknowledg(?:e)?ments|notes|glossary|" +
        @"anexos?|ap[ée]ndices?|agradecimientos|notas|glosario)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex AfterReferencesRegex();

    /// <summary>
    /// Where the reference list begins and ends, or null when the document does not announce one.
    /// The last heading wins: a paper that mentions "References" in its own text still ends with the
    /// real thing.
    /// </summary>
    public static TextSpan? FindReferenceSection(string text)
    {
        var headings = ReferenceHeadingRegex().Matches(text);
        if (headings.Count == 0) return null;

        var heading = headings[^1];
        int start = heading.Index + heading.Length;
        if (start >= text.Length) return null;

        int end = text.Length;
        foreach (Match after in AfterReferencesRegex().Matches(text))
        {
            if (after.Index > start) { end = after.Index; break; }
        }

        return end > start ? new TextSpan(start, end - start) : null;
    }

    // ---- entries -----------------------------------------------------------------------------------

    [GeneratedRegex(@"^[ \t]*(?:\[(\d{1,3})\]|\((\d{1,3})\)|(\d{1,3})[.)])[ \t]+", RegexOptions.Compiled)]
    private static partial Regex EntryNumberRegex();

    [GeneratedRegex(@"(?<![0-9])(1[5-9]\d{2}|20\d{2}|21\d{2})(?![0-9])", RegexOptions.Compiled)]
    private static partial Regex YearRegex();

    /// <summary>A DOI as the standard defines it: registrant <c>10.NNNN</c> and a non-empty suffix.</summary>
    [GeneratedRegex(@"\b10\.\d{4,9}/[^\s""'<>,;)\]}]+", RegexOptions.Compiled)]
    private static partial Regex DoiRegex();

    /// <summary>
    /// A link or a DOI. Stripped before any year is read, because a DOI is full of digits that look
    /// like years — "10.1080/aie.2022.4471" carries a 2022 that has nothing to do with when the work
    /// was published. Reading it as one both split wrapped entries in the wrong place and could have
    /// reported a perfectly ordinary reference as dated in the future.
    /// </summary>
    [GeneratedRegex(@"https?://\S+|10\.\d{1,9}/\S+|doi:\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    private static string WithoutLinks(string value) => LinkRegex().Replace(value, " ");

    /// <summary>Anything shaped like someone trying to write a DOI, valid or not.</summary>
    [GeneratedRegex(@"\b10\.\d{1,9}/[^\s""'<>,;)\]}]*", RegexOptions.Compiled)]
    private static partial Regex DoiCandidateRegex();

    /// <summary>
    /// Splits the reference section into entries.
    ///
    /// A numbered list is delimited by its numbers, which is unambiguous. Everything else falls back
    /// to lines, merging a line into the one above when it does not look like the start of an entry —
    /// which is how a wrapped bibliography survives being pulled out of a PDF. An entry then has to
    /// carry a year, a DOI or a URL to count at all, so a stray sentence under the heading never
    /// becomes a reference.
    /// </summary>
    public static List<Reference> ReadEntries(string text, TextSpan section)
    {
        var lines = SplitLines(text, section);
        bool numbered = lines.Count(l => EntryNumberRegex().IsMatch(l.Text)) >= Math.Max(2, lines.Count / 2);

        var blocks = new List<(string Text, int Start, int Line)>();
        foreach (var line in lines)
        {
            bool starts = numbered ? EntryNumberRegex().IsMatch(line.Text) : LooksLikeEntryStart(line.Text);
            if (starts || blocks.Count == 0)
                blocks.Add(line);
            else
                blocks[^1] = (blocks[^1].Text + " " + line.Text.Trim(), blocks[^1].Start, blocks[^1].Line);
        }

        var references = new List<Reference>();
        foreach (var (raw, start, line) in blocks)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length < 12 || !CarriesSourceEvidence(trimmed)) continue;

            var numberMatch = EntryNumberRegex().Match(trimmed);
            int? number = numberMatch.Success
                ? int.Parse(numberMatch.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value)
                : null;

            var doi = DoiRegex().Match(trimmed);
            var year = YearRegex().Match(WithoutLinks(trimmed));

            references.Add(new Reference
            {
                Raw = trimmed,
                Span = new TextSpan(start, raw.Length),
                Line = line,
                Number = number,
                Year = year.Success ? int.Parse(year.Value) : null,
                Doi = doi.Success ? doi.Value.TrimEnd('.') : null,
                LeadSurname = LeadSurname(trimmed),
            });
        }

        return references;
    }

    /// <summary>A reference names a work, so it carries a year, a DOI or a link. Prose does not.</summary>
    private static bool CarriesSourceEvidence(string entry) =>
        YearRegex().IsMatch(WithoutLinks(entry))
        || DoiCandidateRegex().IsMatch(entry)
        || entry.Contains("http", StringComparison.OrdinalIgnoreCase);

    /// <summary>An author block: "Smith, J." / "Smith, John" / "J. K. Smith" / "Hernández-Silva, M."</summary>
    [GeneratedRegex(
        @"^\s*(?:\p{Lu}[\p{L}'’\-]+,\s*(?:\p{Lu}\.|\p{Lu}\p{Ll}+)|(?:\p{Lu}\.\s*){1,3}\p{Lu}[\p{L}'’\-]+)",
        RegexOptions.Compiled)]
    private static partial Regex AuthorOpeningRegex();

    /// <summary>
    /// Whether a line begins a new entry or continues the one above.
    ///
    /// This is the single most consequential guess in the file, because a bibliography pulled out of a
    /// PDF arrives wrapped with its hanging indent gone. Splitting one entry into two invents a
    /// reference that nobody wrote, and that fragment then gets reported as listed-but-never-cited —
    /// a complaint about a line the author never authored.
    ///
    /// So the test is not "does this look like a sentence", which a journal name passes ("Journal of
    /// Educational Measurement, 59(4), 512-538." opens with a capital and holds a comma). It is "does
    /// this open with an author, or carry a publication year". A continuation line does neither.
    /// </summary>
    private static bool LooksLikeEntryStart(string line)
    {
        var t = line.TrimStart();
        if (t.Length == 0) return false;
        if (EntryNumberRegex().IsMatch(line)) return true;
        if (!char.IsUpper(t[0])) return false;

        var head = WithoutLinks(t);
        head = head[..Math.Min(100, head.Length)];
        return AuthorOpeningRegex().IsMatch(t) || YearRegex().IsMatch(head);
    }

    [GeneratedRegex(@"\p{Lu}[\p{L}'’\-]{1,}", RegexOptions.Compiled)]
    private static partial Regex CapitalisedWordRegex();

    /// <summary>
    /// The surname the entry is filed under: the first capitalised word of the author block, which is
    /// the part before the year or the first parenthesis. Best-effort and only ever used for display —
    /// no check depends on getting it right.
    /// </summary>
    private static string? LeadSurname(string entry)
    {
        var authors = WithoutLinks(entry);
        var yearAt = YearRegex().Match(authors);
        if (yearAt.Success) authors = authors[..yearAt.Index];

        var number = EntryNumberRegex().Match(authors);
        if (number.Success) authors = authors[number.Length..];

        foreach (Match word in CapitalisedWordRegex().Matches(authors))
            if (word.Length > 1) return word.Value;

        return null;
    }

    private static List<(string Text, int Start, int Line)> SplitLines(string text, TextSpan section)
    {
        var result = new List<(string, int, int)>();
        int line = CountLines(text, section.Start);
        int i = section.Start;
        int end = Math.Min(section.End, text.Length);

        while (i < end)
        {
            int nl = text.IndexOf('\n', i);
            int stop = nl < 0 || nl > end ? end : nl;
            var slice = text[i..stop].TrimEnd('\r');
            if (slice.Trim().Length > 0) result.Add((slice, i, line));
            line++;
            i = stop + 1;
        }
        return result;
    }

    // ---- in-text citations --------------------------------------------------------------------------

    [GeneratedRegex(@"\[(\d{1,3}(?:\s*[,–—-]\s*\d{1,3})*)\]", RegexOptions.Compiled)]
    private static partial Regex NumberedCitationRegex();

    /// <summary>A parenthetical group short enough to be a citation and containing a year.</summary>
    [GeneratedRegex(@"\(([^()]{2,180})\)", RegexOptions.Compiled)]
    private static partial Regex ParentheticalRegex();

    /// <summary>Narrative form: <c>Smith (2020)</c>, <c>Smith et al. (2020)</c>, <c>Smith y otros (2020)</c>.</summary>
    [GeneratedRegex(
        @"\b(\p{Lu}[\p{L}'’\-]{1,})(?:\s+(?:et\s+al\.?|y\s+otros|and\s+colleagues))?\s*\((\d{4})[a-z]?\)",
        RegexOptions.Compiled)]
    private static partial Regex NarrativeCitationRegex();

    /// <summary>
    /// Capitalised words that sit in front of a year without being anybody's name. Every one of these
    /// would otherwise become "cited but not listed" — an accusation manufactured out of a caption.
    /// </summary>
    private static readonly HashSet<string> NotSurnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "figure", "fig", "equation", "eq", "section", "chapter", "appendix", "annex",
        "volume", "vol", "edition", "part", "page", "no", "supplement",
        "tabla", "figura", "ecuación", "ecuacion", "sección", "seccion", "capítulo", "capitulo",
        "anexo", "apéndice", "apendice", "volumen", "edición", "edicion", "parte", "página", "pagina",
        "january", "february", "march", "april", "may", "june", "july", "august", "september",
        "october", "november", "december",
        "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto",
        "septiembre", "setiembre", "octubre", "noviembre", "diciembre",
    };

    /// <summary>Inside a parenthetical: a surname, then optional co-authors, then the year.</summary>
    [GeneratedRegex(
        @"^\s*(?:see|v[ée]ase|cf\.?|e\.g\.?,?|p\.\s?ej\.?,?)?\s*" +
        @"(\p{Lu}[\p{L}'’\-]{1,})[^)\d]{0,60}?(\d{4})[a-z]?\b",
        RegexOptions.Compiled)]
    private static partial Regex ParentheticalCitationRegex();

    /// <summary>
    /// Every citation in the running text — that is, everything before the reference section, because
    /// the entries inside the list are not citations of anything.
    /// </summary>
    public static List<InTextCitation> ReadCitations(string text, TextSpan? section)
    {
        int limit = section?.Start ?? text.Length;
        var body = text[..Math.Min(limit, text.Length)];
        var citations = new List<InTextCitation>();

        foreach (Match m in NumberedCitationRegex().Matches(body))
        {
            foreach (var number in ExpandNumbers(m.Groups[1].Value))
            {
                citations.Add(new InTextCitation
                {
                    Raw = m.Value,
                    Span = new TextSpan(m.Index, m.Length),
                    Line = CountLines(text, m.Index),
                    Number = number,
                });
            }
        }

        foreach (Match group in ParentheticalRegex().Matches(body))
        {
            // "(Smith, 2020; Jones, 2019)" is two citations sharing one pair of brackets.
            foreach (var part in group.Groups[1].Value.Split(';'))
            {
                var inner = ParentheticalCitationRegex().Match(part);
                if (!inner.Success || NotSurnames.Contains(inner.Groups[1].Value)) continue;

                citations.Add(new InTextCitation
                {
                    Raw = part.Trim(),
                    Span = new TextSpan(group.Index, group.Length),
                    Line = CountLines(text, group.Index),
                    Surname = inner.Groups[1].Value,
                    Year = int.Parse(inner.Groups[2].Value),
                });
            }
        }

        foreach (Match m in NarrativeCitationRegex().Matches(body))
        {
            if (NotSurnames.Contains(m.Groups[1].Value)) continue;

            // Skip one already caught as a parenthetical, which happens for "(see Smith 2020)".
            if (citations.Any(c => c.Surname == m.Groups[1].Value && c.Span.Start <= m.Index && c.Span.End >= m.Index))
                continue;

            citations.Add(new InTextCitation
            {
                Raw = m.Value,
                Span = new TextSpan(m.Index, m.Length),
                Line = CountLines(text, m.Index),
                Surname = m.Groups[1].Value,
                Year = int.Parse(m.Groups[2].Value),
            });
        }

        return citations;
    }

    /// <summary>"[1,3-5]" cites 1, 3, 4 and 5.</summary>
    private static IEnumerable<int> ExpandNumbers(string list)
    {
        foreach (var part in list.Split(','))
        {
            var range = part.Split(['-', '–', '—'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (range.Length == 2 && int.TryParse(range[0], out int from) && int.TryParse(range[1], out int to)
                && to >= from && to - from < 200)
            {
                for (int n = from; n <= to; n++) yield return n;
            }
            else if (int.TryParse(part.Trim(), out int single))
            {
                yield return single;
            }
        }
    }

    // ---- shared -------------------------------------------------------------------------------------

    public static IEnumerable<Match> DoiCandidates(string text) => DoiCandidateRegex().Matches(text);

    public static bool IsWellFormedDoi(string candidate) =>
        DoiRegex().Match(candidate) is { Success: true } m && m.Length == candidate.Length;

    public static int CountLines(string text, int index)
    {
        int line = 1;
        int stop = Math.Min(index, text.Length);
        for (int i = 0; i < stop; i++)
            if (text[i] == '\n') line++;
        return line;
    }
}

using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Citations;

/// <summary>How a document points at its sources.</summary>
public enum CitationStyle
{
    /// <summary>No citations found at all.</summary>
    None,

    /// <summary>Bracketed numbers — <c>[12]</c>, <c>[1,4]</c>, <c>[3-7]</c>. IEEE, Vancouver, Nature.</summary>
    Numbered,

    /// <summary>Author and year — <c>(Smith, 2020)</c>, <c>Smith et al. (2020)</c>. APA, MLA, Chicago.</summary>
    AuthorYear,

    /// <summary>Both, which is itself worth noticing in a document that should pick one.</summary>
    Mixed,
}

/// <summary>
/// A problem a document has with its own sources.
///
/// Every kind here is decided by looking at the document alone — no network, no index, no model. That
/// is deliberate: a bibliography that was invented usually fails against *itself* before anyone gets
/// round to checking whether the papers exist. Nothing in this list is a probability.
/// </summary>
public enum CitationIssueKind
{
    /// <summary>The text cites a source that appears nowhere in the reference list.</summary>
    CitedButNotListed,

    /// <summary>The reference list carries an entry the text never cites.</summary>
    ListedButNotCited,

    /// <summary>A DOI that cannot be a DOI — the registrant or the suffix is malformed.</summary>
    MalformedDoi,

    /// <summary>Two different references carrying the same DOI. A DOI identifies one work.</summary>
    RepeatedDoi,

    /// <summary>A publication year that has not happened yet.</summary>
    ImpossibleYear,

    /// <summary>The same reference listed more than once.</summary>
    DuplicateReference,
}

/// <summary>One entry of the reference list.</summary>
public sealed record Reference
{
    /// <summary>The entry exactly as it appears, so a reader can find it.</summary>
    public required string Raw { get; init; }

    public required TextSpan Span { get; init; }

    /// <summary>1-based line, to match what an editor shows.</summary>
    public required int Line { get; init; }

    /// <summary>Its position in a numbered list, when the list is numbered.</summary>
    public int? Number { get; init; }

    /// <summary>The publication year, when one could be read out of the entry.</summary>
    public int? Year { get; init; }

    /// <summary>The DOI as written, without the <c>https://doi.org/</c> prefix.</summary>
    public string? Doi { get; init; }

    /// <summary>
    /// The surname the entry is filed under — the first thing before the first comma or year. This is
    /// what an author-year citation has to match, and nothing more is claimed about it.
    /// </summary>
    public string? LeadSurname { get; init; }
}

/// <summary>One pointer from the running text to a source.</summary>
public sealed record InTextCitation
{
    public required string Raw { get; init; }

    public required TextSpan Span { get; init; }

    public required int Line { get; init; }

    /// <summary>The number cited, for a numbered style.</summary>
    public int? Number { get; init; }

    /// <summary>The surname cited, for an author-year style.</summary>
    public string? Surname { get; init; }

    public int? Year { get; init; }
}

/// <summary>A single problem, with the text that shows it and where to look.</summary>
public sealed record CitationIssue
{
    public required CitationIssueKind Kind { get; init; }

    public required TextSpan Span { get; init; }

    public required int Line { get; init; }

    /// <summary>What the issue is about — the citation, the DOI, the entry.</summary>
    public required string Subject { get; init; }

    public required string Message { get; init; }

    /// <summary>
    /// True for the kinds that are a plain contradiction in the document rather than an untidiness. A
    /// cited source missing from the list is one; an uncited entry in the list is not, because people
    /// legitimately list further reading.
    /// </summary>
    public required bool IsContradiction { get; init; }
}

/// <summary>
/// What a document says about its own sources, and where it disagrees with itself.
/// Like the character-artifact report, this never touches the score.
/// </summary>
public sealed record CitationReport
{
    public required IReadOnlyList<Reference> References { get; init; }

    public required IReadOnlyList<InTextCitation> Citations { get; init; }

    public required IReadOnlyList<CitationIssue> Issues { get; init; }

    public required CitationStyle Style { get; init; }

    /// <summary>
    /// False when no reference section could be identified. The cross-checks are then not run at all,
    /// because "cited but not listed" is meaningless when there is no list — and guessing where a
    /// bibliography starts would produce accusations out of formatting.
    /// </summary>
    public required bool HasReferenceList { get; init; }

    public required string Summary { get; init; }

    /// <summary>What the reader should do with it. Always present when anything was found.</summary>
    public required string Advice { get; init; }

    /// <summary>Issues that are a plain contradiction, not an untidiness.</summary>
    public int ContradictionCount { get; init; }

    public bool Any => References.Count > 0 || Citations.Count > 0;

    public bool HasIssues => Issues.Count > 0;

    public static CitationReport Empty { get; } = new()
    {
        References = [],
        Citations = [],
        Issues = [],
        Style = CitationStyle.None,
        HasReferenceList = false,
        Summary = string.Empty,
        Advice = string.Empty,
        ContradictionCount = 0,
    };
}

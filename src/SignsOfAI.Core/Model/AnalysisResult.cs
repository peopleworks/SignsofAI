using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Citations;

namespace SignsOfAI.Core.Model;

/// <summary>Per-category rollup shown in the score breakdown.</summary>
public sealed record CategoryScore(SignCategory Category, double Score, int FindingCount);

/// <summary>The complete result of analyzing a document.</summary>
public sealed record AnalysisResult
{
    /// <summary>The language of the text: given by the caller, or detected.</summary>
    public required string Language { get; init; }

    /// <summary>
    /// The language whose rule pack actually supplied the tells. Equal to <see cref="Language"/>
    /// except when that language has no pack yet, in which case the English catalog was used.
    ///
    /// The two must not be conflated. Running English rules over French prose finds few tells, and
    /// reporting that as a French analysis would present "nothing fired" as a result when nothing
    /// French was ever looked for. Rule packs are files anyone can contribute, so a language without
    /// one is an ordinary state that hosts should describe rather than an error.
    /// </summary>
    public string RulePackLanguage { get; init; } = "";

    /// <summary>
    /// Everything that matched, ordered by position in the text — both the findings that count as
    /// evidence and the ones the writer is using at a rate people write at. Highlighting works from
    /// this list, and so does the rewriter, which should offer to replace a word regardless of whether
    /// its rate proves anything.
    ///
    /// <b>Do not count this to report how many signals were found.</b> Use <see cref="Signals"/>. The
    /// two differ exactly when the genre gate has marked something, and a host that counts this list
    /// will contradict its own category tallies and its own score.
    /// </summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// The findings that are evidence: everything except what the text uses at a human rate. This is
    /// what "N signals" means, it is what <see cref="CategoryScores"/> counts, and it is what moved
    /// <see cref="OverallScore"/>.
    ///
    /// Derived here rather than left to each host on purpose. The distinction arrived as a flag on
    /// <see cref="Finding"/>, and within a day the five hosts disagreed about it: one counted
    /// correctly, one reported a headline that its own category chips contradicted, and the MCP server
    /// handed an agent, as "why this reads like AI", matches the engine had already ruled out. A
    /// semantic that every consumer has to remember is a semantic that some consumer will forget.
    /// </summary>
    public IReadOnlyList<Finding> Signals => field ??= [.. Findings.Where(f => !f.AtHumanRate)];

    /// <summary>
    /// What matched but is not evidence: rules this text uses at a rate measured on writing published
    /// before generative models existed. Worth showing — "you use 'furthermore' about as often as
    /// other people do" is a useful thing to be told — and worth nothing to the score.
    /// </summary>
    public IReadOnlyList<Finding> Observations => field ??= [.. Findings.Where(f => f.AtHumanRate)];

    /// <summary>Score contribution per category (0–100), counting <see cref="Signals"/> only.</summary>
    public required IReadOnlyList<CategoryScore> CategoryScores { get; init; }

    /// <summary>Overall "reads like AI" score, 0 (human) – 100 (unmistakably AI).</summary>
    public required double OverallScore { get; init; }

    public required TextStatistics Statistics { get; init; }

    /// <summary>
    /// Characters found in the file that writing it does not produce — invisible characters, letters
    /// borrowed from another script to impersonate Latin ones.
    ///
    /// Deliberately outside <see cref="Findings"/> and with no effect on <see cref="OverallScore"/>.
    /// The score is a judgement about how the prose reads; this is a list of things that are either
    /// present at a given offset or are not, and mixing the two would turn a checkable fact back into
    /// an opinion. Empty for almost every document.
    /// </summary>
    public ArtifactReport Artifacts { get; init; } = ArtifactReport.Empty;

    /// <summary>
    /// What the document says about its own sources, and where it disagrees with itself — a citation
    /// with no matching entry, a DOI on two different works, a year that has not happened.
    ///
    /// Outside <see cref="Findings"/> and with no effect on <see cref="OverallScore"/>, for the same
    /// reason as <see cref="Artifacts"/>: a teacher cannot take a percentage to an integrity
    /// committee, but "this source is missing from its own bibliography" settles itself in one
    /// question. Empty for any document without references.
    /// </summary>
    public CitationReport Citations { get; init; } = CitationReport.Empty;

    /// <summary>
    /// Human-readable one-line verdict derived from <see cref="OverallScore"/>, in English.
    ///
    /// English-only on purpose: this is what a machine consumer gets — the CLI's `--json`, the MCP
    /// tool's payload — where a stable string is more use than a translated one. Anything shown to a
    /// person goes through the interface's localiser or the report's own resources, both of which
    /// take their boundary from <see cref="VerdictBands"/> exactly as this does.
    /// </summary>
    public string Verdict => VerdictBands.Emphasis(OverallScore) switch
    {
        VerdictEmphasis.High => "Strong signs of AI writing",
        VerdictEmphasis.Elevated => "Moderate signs of AI writing",
        VerdictEmphasis.Present => "Light signs of AI writing",
        _ => "Reads mostly human",
    };
}

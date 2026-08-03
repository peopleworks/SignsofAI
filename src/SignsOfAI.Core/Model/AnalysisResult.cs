using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Citations;

namespace SignsOfAI.Core.Model;

/// <summary>Per-category rollup shown in the score breakdown.</summary>
public sealed record CategoryScore(SignCategory Category, double Score, int FindingCount);

/// <summary>The complete result of analyzing a document.</summary>
public sealed record AnalysisResult
{
    /// <summary>Language code actually used for analysis ("en" or "es").</summary>
    public required string Language { get; init; }

    /// <summary>All findings, ordered by position in the text.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>Score contribution per category (0–100).</summary>
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

    /// <summary>Human-readable one-line verdict derived from <see cref="OverallScore"/>.</summary>
    public string Verdict => OverallScore switch
    {
        >= 70 => "Strong signs of AI writing",
        >= 45 => "Moderate signs of AI writing",
        >= 20 => "Light signs of AI writing",
        _ => "Reads mostly human",
    };
}

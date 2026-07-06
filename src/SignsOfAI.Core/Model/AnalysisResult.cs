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

    /// <summary>Human-readable one-line verdict derived from <see cref="OverallScore"/>.</summary>
    public string Verdict => OverallScore switch
    {
        >= 70 => "Strong signs of AI writing",
        >= 45 => "Moderate signs of AI writing",
        >= 20 => "Light signs of AI writing",
        _ => "Reads mostly human",
    };
}

namespace SignsOfAI.Core.Model;

/// <summary>
/// A single detected "sign of AI writing" together with an actionable recommendation.
/// The whole point of the tool: every finding carries a <see cref="Suggestion"/>, not just a flag.
/// </summary>
public sealed record Finding
{
    /// <summary>Stable rule identifier (e.g. "lex.delve", "rhet.not-just"). Useful for suppression/telemetry.</summary>
    public required string RuleId { get; init; }

    public required SignCategory Category { get; init; }

    public required Severity Severity { get; init; }

    /// <summary>Where in the source text this occurs (for highlighting). May be empty for document-level findings.</summary>
    public required TextSpan Span { get; init; }

    /// <summary>The exact text that triggered the rule.</summary>
    public string MatchedText { get; init; } = string.Empty;

    /// <summary>What is wrong / why this reads as AI.</summary>
    public required string Message { get; init; }

    /// <summary>How to fix it — the actionable recommendation.</summary>
    public required string Suggestion { get; init; }

    /// <summary>Optional evidence, e.g. "48× more frequent post-ChatGPT".</summary>
    public string? Evidence { get; init; }

    /// <summary>Contribution of this finding to the overall score (higher = stronger AI signal).</summary>
    public double Weight { get; init; }
}

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

    /// <summary>
    /// True when this rule is being used in this text at a rate people write at, measured against
    /// writing published before generative models existed. The finding still describes something real
    /// and is still shown, but it is not evidence of a machine and contributes nothing to the score.
    ///
    /// It is marked rather than removed on purpose. Ninety human academic papers produced a median of
    /// seven flagged tells each, which is the sort of thing that teaches a reader to disbelieve the
    /// eighth — but a tool whose whole argument is *showing the evidence* cannot answer that by hiding
    /// evidence. "Furthermore appears once in three thousand words, which is how people write" is a
    /// more useful thing to tell someone than silence, and it keeps the finding available to the
    /// rewriter for a writer whose goal is removing the word rather than proving anything.
    /// </summary>
    public bool AtHumanRate { get; init; }
}

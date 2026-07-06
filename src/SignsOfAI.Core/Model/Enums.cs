namespace SignsOfAI.Core.Model;

/// <summary>How strongly a finding suggests AI authorship / how much it hurts the prose.</summary>
public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>The family of "signs of AI writing" a finding belongs to.</summary>
public enum SignCategory
{
    /// <summary>Overused vocabulary (delve, tapestry, nuanced…).</summary>
    Lexical,

    /// <summary>Rhetorical crutches: rule of three, negative parallelisms, false ranges, hedging.</summary>
    Rhetorical,

    /// <summary>Structural tells: participial clauses, nominalization, copula avoidance.</summary>
    Syntactic,

    /// <summary>Distributional signals such as low burstiness / uniform sentence length.</summary>
    Statistical,
}

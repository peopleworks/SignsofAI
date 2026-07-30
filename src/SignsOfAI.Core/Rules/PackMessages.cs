namespace SignsOfAI.Core.Rules;

/// <summary>
/// The wording the computed analyzers emit, as templates a rule-pack can replace.
///
/// Pattern rules have carried their own <c>message</c> since the beginning, which is why a Spanish
/// pattern finding reads in Spanish. The other three analyzers built their text in C# instead, so
/// analysing Spanish produced an English sentence wrapped around a Spanish word — and no translator
/// could fix it with a pull request, because it was in the binary.
///
/// These are the defaults. A pack that omits <c>messages</c>, including every custom catalog people
/// already have in their browser, behaves exactly as before.
/// </summary>
public static class PackMessages
{
    public const string LexicalOverused = "lexical.overused";
    public const string LexicalSuggestion = "lexical.suggestion";
    public const string BurstinessMessage = "burstiness.message";
    public const string BurstinessSuggestion = "burstiness.suggestion";
    public const string BurstinessEvidence = "burstiness.evidence";
    public const string EmDashMessage = "emdash.message";
    public const string EmDashSuggestion = "emdash.suggestion";
    public const string EmDashEvidence = "emdash.evidence";

    /// <summary>The label every lexical rule gets on the catalog page, which has no message of its own.</summary>
    public const string CatalogLexical = "catalog.lexical";

    /// <summary>How many <c>{n}</c> placeholders each template takes. Guarded by a test.</summary>
    public static IReadOnlyDictionary<string, int> Arity { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [LexicalOverused] = 1,      // {0} the word
        [LexicalSuggestion] = 1,    // {0} the rule's own suggestion
        [BurstinessMessage] = 2,    // {0} burstiness, {1} mean sentence length
        [BurstinessSuggestion] = 0,
        [BurstinessEvidence] = 0,
        [EmDashMessage] = 3,        // {0} dashes, {1} words, {2} per hundred
        [EmDashSuggestion] = 0,
        [EmDashEvidence] = 0,
        [CatalogLexical] = 0,
    };

    /// <summary>English, and byte-identical to what these analyzers produced before.</summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [LexicalOverused] = "“{0}” is heavily overused in AI writing.",
        [LexicalSuggestion] = "Consider: {0}",
        [BurstinessMessage] = "Uniform sentence rhythm (burstiness {0}, mean {1} words). " +
                              "Machine-generated text tends to hold a steady 15–25 word cadence.",
        [BurstinessSuggestion] = "Vary sentence length deliberately: follow a long, clause-heavy sentence " +
                                 "with a short, punchy fragment. Let the rhythm breathe.",
        [BurstinessEvidence] = "Human prose typically scores 0.6–0.8; default LLM output 0.0–0.2.",
        [EmDashMessage] = "Em-dash overuse ({0} in {1} words, {2}/100). " +
                          "LLMs lean on the em-dash as a rhythm crutch.",
        [EmDashSuggestion] = "Replace most with a period, comma, or parentheses; keep em-dashes rare and deliberate.",
        [EmDashEvidence] = "Human prose averages well under one em-dash per 100 words.",
        [CatalogLexical] = "Overused AI vocabulary.",
    };
}

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

    // The character-artifact report. Its wording carries more weight than any other text the engine
    // produces, because it is the one report a reader could mistake for an accusation. Every string
    // here describes what is in the file and stops there.
    public const string ArtifactInvisible = "artifact.invisible";
    public const string ArtifactBidi = "artifact.bidi";
    public const string ArtifactLookalike = "artifact.lookalike";
    public const string ArtifactSpace = "artifact.space";
    public const string ArtifactSoftHyphen = "artifact.soft-hyphen";
    public const string ArtifactVariationSelector = "artifact.variation-selector";
    public const string ArtifactPrivateUse = "artifact.private-use";
    public const string ArtifactTag = "artifact.tag";
    public const string ArtifactSummaryIncidental = "artifact.summary.incidental";
    public const string ArtifactSummarySystematic = "artifact.summary.systematic";
    public const string ArtifactAdvice = "artifact.advice";

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
        [ArtifactInvisible] = 2,            // {0} codepoint, {1} character name
        [ArtifactBidi] = 2,
        [ArtifactLookalike] = 4,            // {0} codepoint, {1} name, {2} the Latin letter, {3} the word
        [ArtifactSpace] = 2,
        [ArtifactSoftHyphen] = 2,
        [ArtifactVariationSelector] = 2,
        [ArtifactPrivateUse] = 2,
        [ArtifactTag] = 2,
        [ArtifactSummaryIncidental] = 1,    // {0} how many
        [ArtifactSummarySystematic] = 3,    // {0} how many, {1} sections affected, {2} sections total
        [ArtifactAdvice] = 0,
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
        [ArtifactInvisible] = "{1} ({0}) — a character that occupies no space when the text is displayed.",
        [ArtifactBidi] = "{1} ({0}) — a control character that can make the displayed text differ from the stored text.",
        [ArtifactLookalike] = "“{3}” contains {1} ({0}) where the Latin letter “{2}” belongs. " +
                              "The two are indistinguishable on screen.",
        [ArtifactSpace] = "{1} ({0}) in place of an ordinary space.",
        [ArtifactSoftHyphen] = "{1} ({0}) — an invisible hyphenation point, routine in text copied out of a PDF.",
        [ArtifactVariationSelector] = "{1} ({0}) — a rendering modifier attached to something that is not an emoji.",
        [ArtifactPrivateUse] = "{1} ({0}) — a codepoint with no meaning outside the font that defined it.",
        [ArtifactTag] = "{1} ({0}) — an invisible character of the kind used to carry hidden text alongside visible text.",
        [ArtifactSummaryIncidental] = "{0} unusual characters, not spread through the document. " +
                                      "Copying from a web page or a PDF produces these.",
        [ArtifactSummarySystematic] = "{0} characters that typing does not produce, spread across {1} of {2} " +
                                      "sections of the document. That distribution is what a tool leaves behind " +
                                      "when it processes a whole text.",
        [ArtifactAdvice] = "This says nothing about who wrote the text, and it is not evidence of dishonesty. " +
                           "It is a question about where the file has been: ask the writer to open the document " +
                           "and describe how it was produced.",
    };
}

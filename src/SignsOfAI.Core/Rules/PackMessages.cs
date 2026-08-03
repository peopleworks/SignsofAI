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

    // The citation report. Same standard as the artifact wording: it describes what the document says
    // about its own sources and stops there. "Missing from the bibliography" is a fact; "invented" is
    // a conclusion that belongs to the person holding the meeting, not to us.
    public const string CitationNotListed = "citation.not-listed";
    public const string CitationNumberNotListed = "citation.number-not-listed";
    public const string CitationNotCited = "citation.not-cited";
    public const string CitationMalformedDoi = "citation.malformed-doi";
    public const string CitationRepeatedDoi = "citation.repeated-doi";
    public const string CitationImpossibleYear = "citation.impossible-year";
    public const string CitationDuplicate = "citation.duplicate";
    public const string CitationSummaryClean = "citation.summary.clean";
    public const string CitationSummaryIssues = "citation.summary.issues";
    public const string CitationSummaryNoList = "citation.summary.nolist";
    public const string CitationAdvice = "citation.advice";

    // The per-writer baseline. The most consequential wording in the product: it is the one report
    // that could be read as naming a culprit, and it must never read that way. Every string here
    // describes a distance and its scale, and says out loud what a distance cannot settle.
    public const string StyleSummaryWithin = "style.summary.within";
    public const string StyleSummaryEdge = "style.summary.edge";
    public const string StyleSummaryBeyond = "style.summary.beyond";
    public const string StyleNoteOutside = "style.note.outside";
    public const string StyleNoteBroad = "style.note.broad";
    public const string StyleAdvice = "style.advice";
    public const string StyleNeedBaseline = "style.need.baseline";
    public const string StyleNeedQuestioned = "style.need.questioned";
    public const string StyleNeedSamples = "style.need.samples";
    public const string StyleNeedLanguage = "style.need.language";
    public const string StyleAdviceInsufficient = "style.advice.insufficient";

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
        [CitationNotListed] = 1,            // {0} the citation
        [CitationNumberNotListed] = 2,      // {0} the number cited, {1} how many entries exist
        [CitationNotCited] = 1,             // {0} the entry
        [CitationMalformedDoi] = 1,         // {0} the DOI as written
        [CitationRepeatedDoi] = 2,          // {0} the DOI, {1} how many references carry it
        [CitationImpossibleYear] = 2,       // {0} the year given, {1} the current year
        [CitationDuplicate] = 1,            // {0} the entry
        [CitationSummaryClean] = 2,         // {0} references, {1} citations
        [CitationSummaryIssues] = 2,        // {0} contradictions, {1} references
        [CitationSummaryNoList] = 1,        // {0} citations found
        [CitationAdvice] = 0,
        [StyleSummaryWithin] = 2,           // {0} the distance, {1} the writer's own widest gap
        [StyleSummaryEdge] = 2,
        [StyleSummaryBeyond] = 2,
        [StyleNoteOutside] = 2,             // {0} words outside, {1} words measured
        [StyleNoteBroad] = 0,
        [StyleAdvice] = 0,
        [StyleNeedBaseline] = 2,            // {0} words available, {1} words needed
        [StyleNeedQuestioned] = 2,
        [StyleNeedSamples] = 2,             // {0} usable pieces, {1} needed
        [StyleNeedLanguage] = 0,
        [StyleAdviceInsufficient] = 0,
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
        [CitationNotListed] = "“{0}” is cited in the text but appears nowhere in the reference list.",
        [CitationNumberNotListed] = "The text cites [{0}], and the reference list has {1} numbered entries.",
        [CitationNotCited] = "Listed but never cited: “{0}”",
        [CitationMalformedDoi] = "“{0}” cannot be a DOI: a DOI is 10, a four-to-nine digit " +
                                 "registrant, a slash, and a non-empty suffix.",
        [CitationRepeatedDoi] = "{1} references carry the DOI {0}. A DOI identifies one work, so at most " +
                                "one of them is right.",
        [CitationImpossibleYear] = "Dated {0}, which has not happened yet ({1}).",
        [CitationDuplicate] = "The same reference is listed twice: “{0}”",
        [CitationSummaryClean] = "{0} references and {1} citations, and the two agree with each other.",
        [CitationSummaryIssues] = "{0} citation(s) contradict the document’s own reference list of {1} entries.",
        [CitationSummaryNoList] = "{0} citations in the text, and no reference list this could identify. " +
                                  "The cross-checks were not run.",
        [CitationAdvice] = "A source missing from its own bibliography is usually a slip, and it is always " +
                           "the writer’s to explain. Ask for the source itself: a real one can be produced " +
                           "in seconds, and an invented one cannot.",
        [StyleSummaryWithin] = "Distance {0}. This writer’s own pieces sit up to {1} from their centre, " +
                               "so this one is inside the range they already cover.",
        [StyleSummaryEdge] = "Distance {0}, a little past the {1} that this writer’s own pieces cover " +
                             "between themselves.",
        [StyleSummaryBeyond] = "Distance {0}, against {1} for the widest gap between this writer’s own " +
                               "pieces. This one sits outside the range they cover.",
        [StyleNoteOutside] = "{0} of the {1} words measured are used at a rate this writer has never " +
                             "used them at in any of their own pieces.",
        [StyleNoteBroad] = "Those samples also disagree with each other a lot, which makes any comparison " +
                           "weak — check that they are all by the same person and of a similar kind.",
        [StyleAdvice] = "Style moves with the assignment, the genre, the deadline, and with a person simply " +
                        "getting better. A text outside the range is a reason to ask what changed, never a " +
                        "conclusion about who wrote it — and a text inside the range is the more useful " +
                        "result, because it is the one that settles a suspicion.",
        [StyleNeedBaseline] = "Not enough of this writer’s own work to measure against: {0} words, and this " +
                              "needs at least {1}.",
        [StyleNeedQuestioned] = "The text being checked is too short: {0} words, and this needs at least {1}.",
        [StyleNeedSamples] = "Not enough separate pieces to measure the writer’s own variation: {0} usable, " +
                             "and this needs at least {1}.",
        [StyleNeedLanguage] = "Too few of this language’s function words appear in these texts to " +
                              "measure anything. Check that the language selected matches what was written.",
        [StyleAdviceInsufficient] = "A distance computed from too little writing would be noise wearing a " +
                                    "decimal point. Add more of this writer’s earlier work and try again.",
    };
}

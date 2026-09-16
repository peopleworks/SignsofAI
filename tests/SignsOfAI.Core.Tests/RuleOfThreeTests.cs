using SignsOfAI.Core;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The rule-of-three tell is a <em>rhetorical</em> tricolon: three words chosen for cadence, "fast,
/// simple, and powerful". Its regex matched any three word-characters, so it could not tell that
/// cadence from an author listing three values — financial-aid year codes, gene names, the numbers
/// of a dose schedule. #100 found it in a support-ticket reply, then measured it: 18 of the rule's
/// 236 matches in the calibration corpus are lists like that, and six texts had no other hit.
///
/// The narrowing lives in the pack, not in C#, so the whole rule stays editable by anyone: a list is
/// skipped when none of its three items contains a lowercase letter. Two traps shaped the regex, and
/// both are pinned below — the analyzer compiles every pack pattern with IgnoreCase, which would make
/// an uppercase class match prose too, and <c>[A-Z]</c> would miss an accented acronym.
///
/// It changes no published number: the calibration run before and after is identical in every field.
/// </summary>
public class RuleOfThreeTests
{
    private readonly AiWritingAnalyzer _a = new();

    private bool Fires(string text, string lang, string ruleId) =>
        _a.Analyze(text, lang).Findings.Any(f => f.RuleId == ruleId);

    // The seven published-paper lists #100 listed from the corpus, and the two from the ticket.
    [Theory]
    [InlineData("Expression was measured for DPM1, DPM2 and DPM3.")]
    [InlineData("Cells were stained for CD31, CD45, and CD56, as described.")]
    [InlineData("The panel covered ARS, RQR and RRS.")]
    [InlineData("Exposure to IMI, CTD, and THX, alone or combined.")]
    [InlineData("Group sizes were 6, 6 and 7, respectively.")]
    [InlineData("Samples were taken at weeks 25, 26, and 28.")]
    [InlineData("Doses of 10, 15, and 20.")]
    [InlineData("The same is true of 2324, 2425 and 2526.")]
    [InlineData("It drops SENIOR, COMMUNITY, COMM, CMTY and CHD, and matches the rest.")]
    public void A_list_of_data_is_not_a_tricolon_en(string text) =>
        Assert.False(Fires(text, "en", "rhet.rule-of-three"), text);

    [Theory]
    [InlineData("The tool is fast, simple, and powerful.")]
    [InlineData("We invited Alice, Bob and Carol.")]
    // Mixed: one prose item makes it a sentence again. Requiring all three to be data is the
    // conservative choice #100 argued for, and the one its numbers measured.
    [InlineData("They expressed CD31, CD45, and cells.")]
    public void A_rhetorical_tricolon_still_fires_en(string text) =>
        // If IgnoreCase leaked into the token class, lowercase prose would read as data and these
        // would stop firing. That is the trap the inline (?-i:) exists to avoid.
        Assert.True(Fires(text, "en", "rhet.rule-of-three"), text);

    [Theory]
    [InlineData("Se aplicó en los cursos 2324, 2425 y 2526.")]
    [InlineData("Participaron la UE, la OTAN y la ONU; también UE, OTAN y ONU.")]
    // Accented acronyms: \p{Lu} rather than [A-Z], because the Turkish dotless i taught this project
    // what an ASCII-only letter class does to other alphabets.
    [InlineData("Se revisaron ÁREA, ÉPOCA y ÍNDICE.")]
    public void A_list_of_data_is_not_a_tricolon_es(string text) =>
        Assert.False(Fires(text, "es", "rhet.regla-de-tres"), text);

    [Theory]
    [InlineData("La herramienta es rápida, sencilla y potente.")]
    // Single-word items, as the rule has always required: with articles ("el área, la época") it never
    // matched, before or after this change. Lowercase accented letters must still read as prose.
    [InlineData("Revisaron área, época y índice.")]
    [InlineData("Acordaron UE, OTAN y aliados.")]
    public void A_rhetorical_tricolon_still_fires_es(string text) =>
        Assert.True(Fires(text, "es", "rhet.regla-de-tres"), text);
}

using System.Linq;
using SignsOfAI.Core;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The half of the conversation that was never meant to be in the document.
///
/// Every other rule in this project is a judgement about style, and a formal writer can lose to any
/// of them. These are not that. "I hope this helps" at the foot of an essay is not a register the
/// student chose; it is an assistant's closing line, pasted in with the answer — the same kind of
/// claim the character scanner makes, about where the file has been rather than about who is
/// talented.
///
/// That is also why this batch was admissible when most of the pattern set it came from was not.
/// The rules were screened against the calibration corpus first — 249,455 words of English and
/// 39,712 of Spanish, all published before generative models existed — and every one of them scored
/// zero. Re-running the calibration afterwards left the published false-positive rate untouched,
/// which is the point: they cost nothing to carry.
///
/// The mine they came from is amanmaqsood/prose-humanizer (MIT). Twelve of its other candidates
/// were rejected here by the same screen: underpin, optimize, elucidate, paradigm, exemplify and
/// illuminate are ordinary research English, and each appears in three to six of those ninety texts.
///
/// One of the six then failed the screen it had passed. When 206 learner essays joined the corpus,
/// <c>chat.eager-opener</c> fired in eleven of them: its pattern accepted "Of course," with a comma —
/// an ordinary concession — alongside "Certainly!". Zero on published articles had been zero on one
/// register. The pattern now requires the exclamation mark, and the test for the comma is below.
/// </summary>
public class ChatResidueTests
{
    private readonly AiWritingAnalyzer _a = new();

    private bool Has(string text, string lang, string ruleId) =>
        _a.Analyze(text, lang).Findings.Any(f => f.RuleId == ruleId);

    [Theory]
    [InlineData("As an AI language model, I should note that the figures are indicative.", "chat.model-self-reference")]
    [InlineData("As of my last training update, the tidal survey had not been repeated.", "chat.training-cutoff")]
    [InlineData("The wall was built in 1971. I hope this helps.", "chat.signoff")]
    [InlineData("Would you like me to expand the section on sediment transport?", "chat.signoff")]
    [InlineData("I cannot browse the internet, so the citation below is from memory.", "chat.capability-disclaimer")]
    [InlineData("Here is the revised version of your essay on coastal erosion.", "chat.answer-preamble")]
    [InlineData("Certainly! The coastline retreated by nine metres.", "chat.eager-opener")]
    public void Flags_the_assistants_own_turn_en(string text, string ruleId) =>
        Assert.True(Has(text, "en", ruleId), ruleId);

    [Theory]
    [InlineData("Como modelo de lenguaje, debo señalar que las cifras son indicativas.", "chat.model-self-reference")]
    [InlineData("Hasta mi última actualización, el estudio no se había repetido.", "chat.training-cutoff")]
    [InlineData("El muro se construyó en 1971. Espero que esto te ayude.", "chat.signoff")]
    [InlineData("¿Quieres que amplíe el apartado sobre el transporte de sedimentos?", "chat.signoff")]
    [InlineData("No tengo acceso a información en tiempo real sobre las mareas.", "chat.capability-disclaimer")]
    [InlineData("Aquí tienes la versión reescrita de tu ensayo sobre la erosión costera.", "chat.answer-preamble")]
    [InlineData("¡Por supuesto! La costa retrocedió nueve metros.", "chat.eager-opener")]
    public void Flags_the_assistants_own_turn_es(string text, string ruleId) =>
        Assert.True(Has(text, "es", ruleId), ruleId);

    /// <summary>
    /// The regexes have to leave ordinary writing alone, and two of them are close to sentences a
    /// person really writes. A tutor's own feedback says "let me know" and a historian writes "of
    /// course" mid-sentence; neither is an assistant handing back an answer.
    /// </summary>
    [Theory]
    [InlineData("Let me know when the survey is finished and I will read it.", "chat.signoff")]
    [InlineData("The wall was, of course, built long before the survey began.", "chat.eager-opener")]
    [InlineData("Of course, the wall was built long before the survey began.", "chat.eager-opener")]
    [InlineData("Absolutely, the council should publish it before the winter.", "chat.eager-opener")]
    [InlineData("I hope the council publishes the survey before the winter.", "chat.signoff")]
    [InlineData("Here is the revised timetable the committee agreed on Tuesday.", "chat.answer-preamble")]
    public void Leaves_a_person_writing_to_a_person_alone_en(string text, string ruleId) =>
        Assert.False(Has(text, "en", ruleId), ruleId);

    [Theory]
    [InlineData("Avísame cuando termine el estudio y lo leo.", "chat.signoff")]
    [InlineData("El muro, por supuesto, se construyó mucho antes del estudio.", "chat.eager-opener")]
    [InlineData("Por supuesto, el muro se construyó mucho antes del estudio.", "chat.eager-opener")]
    [InlineData("Claro, el consejo debería publicarlo antes del invierno.", "chat.eager-opener")]
    [InlineData("Aquí tienes el calendario que acordó la comisión el martes.", "chat.answer-preamble")]
    public void Leaves_a_person_writing_to_a_person_alone_es(string text, string ruleId) =>
        Assert.False(Has(text, "es", ruleId), ruleId);

    /// <summary>
    /// A rule that fires on nothing measured is a rule with no measured human rate, and the pack
    /// must keep saying so rather than inventing one. See <c>PatternRule.HumanRatePer1000</c>: an
    /// absent rate means "never observed", which is what these are, and a rate of zero would be a
    /// different and much stronger claim.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void The_new_rules_claim_no_human_rate(string lang)
    {
        var pack = SignsOfAI.Core.Rules.RulePackLoader.Load(lang);
        var chat = pack.Patterns.Where(p => p.Id.StartsWith("chat.")).ToList();

        Assert.Equal(6, chat.Count);
        Assert.All(chat, rule => Assert.Null(rule.HumanRatePer1000));
        Assert.All(chat, rule => Assert.False(string.IsNullOrWhiteSpace(rule.Evidence), rule.Id));
    }
}

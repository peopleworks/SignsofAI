using System.Linq;
using SignsOfAI.Core;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Locks in the taxonomy we adopted from the no-ai-slop pattern set: empty intensifiers,
/// weasel attribution, throat-clearing, rhetorical setups, faux-insight, filler phrases,
/// hype, colon reveals, participial padding, negative listing, em-dash overuse and formatting
/// slop — in BOTH languages (our differentiator over an English-only skill).
/// </summary>
public class NoAiSlopParityTests
{
    private readonly AiWritingAnalyzer _a = new();

    private bool Has(string text, string lang, string ruleId) =>
        _a.Analyze(text, lang).Findings.Any(f => f.RuleId == ruleId);

    [Theory]
    [InlineData("Honestly, this is just simply a fundamentally better idea.", "lex.honestly")]
    [InlineData("Honestly, this is just simply a fundamentally better idea.", "lex.just")]
    [InlineData("Honestly, this is just simply a fundamentally better idea.", "lex.simply")]
    [InlineData("Honestly, this is just simply a fundamentally better idea.", "lex.fundamentally")]
    public void Flags_empty_intensifiers_en(string text, string ruleId) =>
        Assert.True(Has(text, "en", ruleId), ruleId);

    [Theory]
    [InlineData("We utilize the tool to facilitate and streamline work.", "lex.utilize")]
    [InlineData("We utilize the tool to facilitate and streamline work.", "lex.facilitate")]
    [InlineData("We utilize the tool to facilitate and streamline work.", "lex.streamline")]
    [InlineData("It is a beacon that will supercharge and empower every team.", "lex.beacon")]
    [InlineData("It is a beacon that will supercharge and empower every team.", "lex.supercharge")]
    [InlineData("It is a beacon that will supercharge and empower every team.", "lex.empower")]
    public void Flags_missing_banned_vocabulary_en(string text, string ruleId) =>
        Assert.True(Has(text, "en", ruleId), ruleId);

    [Theory]
    [InlineData("Experts agree that studies show this is widely regarded as true.", "rhet.weasel-attribution")]
    [InlineData("Here's the thing, let me be clear about the plan.", "rhet.throat-clearing")]
    [InlineData("What if I told you there is a plot twist waiting here?", "rhet.rhetorical-setup")]
    [InlineData("This is what nobody tells you about writing well.", "rhet.faux-insight")]
    [InlineData("At the end of the day, going forward, we will win.", "rhet.end-of-day")]
    [InlineData("At the end of the day, going forward, we will win.", "rhet.going-forward")]
    [InlineData("This is a paradigm shift that changes everything.", "rhet.hype-phrase")]
    [InlineData("The truth: nobody actually reads the footnotes.", "syn.colon-reveal")]
    [InlineData("Revenue grew fast, highlighting the shift in demand.", "syn.superficial-ing")]
    [InlineData("Not a fad. Not a passing trend.", "rhet.negative-listing")]
    public void Flags_new_patterns_en(string text, string ruleId) =>
        Assert.True(Has(text, "en", ruleId), ruleId);

    [Theory]
    [InlineData("Simplemente utilizamos la herramienta para agilizar todo, honestamente.", "lex.simplemente")]
    [InlineData("Simplemente utilizamos la herramienta para agilizar todo, honestamente.", "lex.utilizar")]
    [InlineData("Simplemente utilizamos la herramienta para agilizar todo, honestamente.", "lex.agilizar")]
    [InlineData("Los expertos coinciden y los estudios demuestran que funciona.", "rhet.atribucion-vaga")]
    [InlineData("Seamos honestos: que quede claro desde el principio.", "rhet.carraspeo")]
    [InlineData("Esto es lo que nadie te dice sobre el oficio.", "rhet.falsa-revelacion")]
    [InlineData("Es un cambio de paradigma que lo cambia todo.", "rhet.frase-grandilocuente")]
    [InlineData("La verdad: no funciona como prometen.", "syn.revelacion-dos-puntos")]
    [InlineData("Creció rápido, destacando la tendencia del mercado.", "syn.gerundio-superficial")]
    [InlineData("Al final del día, de cara al futuro, ganaremos.", "rhet.al-final-del-dia")]
    [InlineData("Al final del día, de cara al futuro, ganaremos.", "rhet.de-cara-al-futuro")]
    public void Flags_new_patterns_es(string text, string ruleId) =>
        Assert.True(Has(text, "es", ruleId), ruleId);

    [Fact]
    public void Flags_em_dash_overuse()
    {
        const string dashy =
            "The product is fast — really fast — and the whole team shipped it on time this week. " +
            "Users loved it — every single review said so during the launch quarter, across many regions " +
            "and channels, and the numbers kept climbing steadily through the entire rollout without any slowdown.";

        Assert.True(Has(dashy, "en", "rhet.em-dash"));
    }

    [Fact]
    public void Ignores_a_single_deliberate_em_dash()
    {
        const string ok =
            "The bus was late again and I stood there for a while in the cold rain — the usual story. " +
            "Then it rolled right past without stopping, so I gave up and walked the rest of the way home.";

        Assert.False(Has(ok, "en", "rhet.em-dash"));
    }

    [Theory]
    [InlineData("## Overview \U0001F680\nThe method works **well** in real projects.", "fmt.emoji-heading")]
    [InlineData("## Overview \U0001F680\nThe method works **well** in real projects.", "fmt.midsentence-bold")]
    public void Flags_formatting_slop(string text, string ruleId) =>
        Assert.True(Has(text, "en", ruleId), ruleId);
}

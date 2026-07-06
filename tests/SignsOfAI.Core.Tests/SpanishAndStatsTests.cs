using SignsOfAI.Core;
using SignsOfAI.Core.Text;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class SpanishAndStatsTests
{
    private readonly AiWritingAnalyzer _analyzer = new();

    private const string AiSpanish =
        "En la era digital actual, debemos sumergirnos en el vasto mundo de la innovación. " +
        "Cabe destacar que este enfoque multifacético se erige como un testimonio de progreso. " +
        "No solo es una herramienta, sino también una solución crucial y transformadora.";

    [Fact]
    public void Detects_spanish_language()
    {
        Assert.Equal("es", LanguageDetector.Detect(AiSpanish));
    }

    [Fact]
    public void Flags_spanish_ai_markers()
    {
        var result = _analyzer.Analyze(AiSpanish, "es");

        Assert.Equal("es", result.Language);
        Assert.Contains(result.Findings, f => f.RuleId == "rhet.era-digital");
        Assert.Contains(result.Findings, f => f.RuleId == "rhet.cabe-destacar");
        Assert.Contains(result.Findings, f => f.RuleId == "syn.testimonio-de");
        Assert.Contains(result.Findings, f => f.RuleId == "lex.multifacetico");
    }

    [Fact]
    public void Auto_language_selects_spanish_pack()
    {
        var result = _analyzer.Analyze(AiSpanish); // auto-detect
        Assert.Equal("es", result.Language);
    }

    [Fact]
    public void Uniform_sentences_have_low_burstiness_and_get_flagged()
    {
        // Six sentences of near-identical length → machine-like uniformity.
        const string uniform =
            "The team shipped the feature on time today. " +
            "The users tried the feature right away now. " +
            "The system logged the events without any issue. " +
            "The report showed the numbers were quite steady. " +
            "The manager praised the work in the meeting. " +
            "The client renewed the contract for the year.";

        var result = _analyzer.Analyze(uniform, "en");

        Assert.True(result.Statistics.Burstiness < 0.45,
            $"burstiness {result.Statistics.Burstiness} expected low");
        Assert.Contains(result.Findings, f => f.RuleId == "stat.burstiness");
    }

    [Fact]
    public void Empty_text_does_not_throw()
    {
        var result = _analyzer.Analyze("", "en");
        Assert.Empty(result.Findings);
        Assert.Equal(0, result.OverallScore);
    }
}

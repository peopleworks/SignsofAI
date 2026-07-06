using SignsOfAI.Core;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>Guards the score calibration: a graded ladder of AI-ness must stay ordered and spread.</summary>
public class ScoringTests
{
    private readonly AiWritingAnalyzer _a = new();

    private const string CleanHuman =
        "The bus was late again. I stood there for twelve minutes, coffee going cold, watching the rain " +
        "streak sideways. Then it rolled right past without stopping. So I walked. Forty blocks.";

    private const string LightAi =
        "Our team met on Tuesday to review the quarterly numbers. Sales were up in two regions and flat in " +
        "the third. We agreed to revisit the pricing model and to leverage the new dashboard for weekly check-ins.";

    private const string ModerateAi =
        "In today's digital age, our multifaceted platform serves as a comprehensive solution. It is worth " +
        "noting that this robust tool fosters collaboration and helps teams navigate the ever-changing landscape.";

    private const string StrongAi =
        "In today's digital age, we must delve into the rich tapestry of modern innovation. It's worth noting " +
        "that this multifaceted and nuanced approach serves as a testament to progress. It's not just a tool, " +
        "it's a pivotal, transformative solution that fosters growth and unlocks potential.";

    [Fact]
    public void Scores_increase_monotonically_with_ai_ness()
    {
        double clean = _a.Analyze(CleanHuman, "en").OverallScore;
        double light = _a.Analyze(LightAi, "en").OverallScore;
        double moderate = _a.Analyze(ModerateAi, "en").OverallScore;
        double strong = _a.Analyze(StrongAi, "en").OverallScore;

        Assert.True(clean < light && light < moderate && moderate < strong,
            $"expected clean<light<moderate<strong, got {clean} {light} {moderate} {strong}");
    }

    [Fact]
    public void Mid_range_band_is_populated_not_clumped_at_100()
    {
        // The whole point of the calibration: middling AI text lands in the mid band, not pinned to 100.
        double moderate = _a.Analyze(ModerateAi, "en").OverallScore;
        Assert.InRange(moderate, 45, 95);

        double light = _a.Analyze(LightAi, "en").OverallScore;
        Assert.InRange(light, 15, 55);
    }

    [Fact]
    public void Clean_text_scores_zero()
    {
        Assert.Equal(0, _a.Analyze(CleanHuman, "en").OverallScore);
    }
}

using SignsOfAI.Core;
using SignsOfAI.Core.Model;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class AnalyzerTests
{
    private readonly AiWritingAnalyzer _analyzer = new();

    // A deliberately AI-flavored paragraph.
    private const string AiEnglish =
        "In today's digital age, we must delve into the rich tapestry of modern innovation. " +
        "It's worth noting that this multifaceted and nuanced approach serves as a testament to progress. " +
        "It's not just a tool, it's a pivotal, transformative solution that fosters growth.";

    private const string HumanEnglish =
        "The bus was late again. I waited twelve minutes in the rain, coffee going cold, " +
        "and then it just rolled past without stopping. Typical. So I walked.";

    [Fact]
    public void Flags_classic_ai_vocabulary()
    {
        var result = _analyzer.Analyze(AiEnglish, "en");

        Assert.Equal("en", result.Language);
        Assert.Contains(result.Findings, f => f.RuleId == "lex.delve");
        Assert.Contains(result.Findings, f => f.RuleId == "lex.multifaceted");
        Assert.Contains(result.Findings, f => f.RuleId == "lex.pivotal");
    }

    [Fact]
    public void Flags_rhetorical_and_syntactic_patterns()
    {
        var result = _analyzer.Analyze(AiEnglish, "en");

        Assert.Contains(result.Findings, f => f.RuleId == "rhet.in-todays");
        Assert.Contains(result.Findings, f => f.RuleId == "rhet.not-just");
        Assert.Contains(result.Findings, f => f.RuleId == "rhet.worth-noting");
        Assert.Contains(result.Findings, f => f.RuleId == "syn.rich-tapestry");
    }

    [Fact]
    public void Every_finding_carries_a_suggestion()
    {
        var result = _analyzer.Analyze(AiEnglish, "en");

        Assert.NotEmpty(result.Findings);
        Assert.All(result.Findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Suggestion)));
    }

    [Fact]
    public void Ai_text_scores_higher_than_human_text()
    {
        var ai = _analyzer.Analyze(AiEnglish, "en");
        var human = _analyzer.Analyze(HumanEnglish, "en");

        Assert.True(ai.OverallScore > human.OverallScore,
            $"AI={ai.OverallScore} should exceed human={human.OverallScore}");
        Assert.True(ai.OverallScore >= 45, $"AI score {ai.OverallScore} unexpectedly low");
    }

    [Fact]
    public void Findings_span_maps_back_to_matched_text()
    {
        var result = _analyzer.Analyze(AiEnglish, "en");

        foreach (var f in result.Findings.Where(f => f.Span.Length > 0))
            Assert.Equal(f.MatchedText, f.Span.Slice(AiEnglish));
    }
}

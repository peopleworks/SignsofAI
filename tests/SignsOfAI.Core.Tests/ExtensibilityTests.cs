using SignsOfAI.Core;
using SignsOfAI.Core.Rules;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class ExtensibilityTests
{
    private readonly AiWritingAnalyzer _analyzer = new();

    // A user-supplied catalog: flag the company's banned buzzwords, in any language.
    private const string CustomPackJson =
        """
        {
          "language": "*",
          "lexical": [
            { "id": "custom.synergy", "terms": ["synergy", "synergies"], "weight": 6, "severity": "High", "suggestion": "cooperation, working together" }
          ]
        }
        """;

    [Fact]
    public void Custom_catalog_adds_findings()
    {
        var pack = RulePack.FromJson(CustomPackJson);
        const string text = "We must unlock synergy across the org to move forward together as a team.";

        var withoutCustom = _analyzer.Analyze(text, "en");
        var withCustom = _analyzer.Analyze(text, "en", [pack]);

        Assert.DoesNotContain(withoutCustom.Findings, f => f.RuleId == "custom.synergy");
        Assert.Contains(withCustom.Findings, f => f.RuleId == "custom.synergy");
    }

    [Fact]
    public void Custom_catalog_overrides_a_builtin_rule_by_id()
    {
        // Same id as the built-in "delve" lexical rule → replaces its suggestion.
        var pack = RulePack.FromJson(
            """
            { "language": "en", "lexical": [
              { "id": "lex.delve", "terms": ["delve"], "weight": 6, "suggestion": "MY CUSTOM FIX" } ] }
            """);

        var result = _analyzer.Analyze("We delve into the topic.", "en", [pack]);
        var delve = Assert.Single(result.Findings, f => f.RuleId == "lex.delve");
        Assert.Contains("MY CUSTOM FIX", delve.Suggestion);
    }

    [Fact]
    public void Language_scoped_custom_catalog_is_ignored_for_other_languages()
    {
        var esOnly = RulePack.FromJson(
            """
            { "language": "es", "lexical": [ { "id": "custom.solo-es", "terms": ["innovation"], "suggestion": "x" } ] }
            """);

        var enResult = _analyzer.Analyze("This innovation is great.", "en", [esOnly]);
        Assert.DoesNotContain(enResult.Findings, f => f.RuleId == "custom.solo-es");
    }

    [Fact]
    public void Merge_roundtrips_through_json()
    {
        var pack = RulePack.FromJson(CustomPackJson);
        var json = pack.ToJson();
        var reparsed = RulePack.FromJson(json);
        Assert.Contains(reparsed.Lexical, r => r.Id == "custom.synergy");
    }
}

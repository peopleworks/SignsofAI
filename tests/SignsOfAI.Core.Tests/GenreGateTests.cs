using SignsOfAI.Core;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The gate decides which findings count as evidence of a machine, so the tests that matter are the
/// ones about what it must never do: hide a finding, silence a rule that has no measured rate, or
/// turn a contributed catalog's calibration off by accident.
/// </summary>
public class GenreGateTests
{
    private static RulePack Pack(params (string Id, double Rate)[] rules) => new()
    {
        Language = "en",
        Lexical = [.. rules.Select(r => new LexicalRule
        {
            Id = r.Id,
            Terms = [r.Id],
            Suggestion = "something, else",
            HumanRatePer1000 = r.Rate,
        })],
    };

    private static Finding Hit(string ruleId) => new()
    {
        RuleId = ruleId,
        Category = SignCategory.Lexical,
        Severity = Severity.Low,
        Span = new TextSpan(0, 1),
        Message = "m",
        Suggestion = "s",
        Weight = 2.0,
    };

    [Fact]
    public void Marks_but_never_removes()
    {
        var findings = new[] { Hit("lex.a"), Hit("lex.a") };

        var result = GenreGate.Apply(findings, Pack(("lex.a", 5.0)), wordCount: 1000);

        // Two hits in a thousand words is 2.0, well under the threshold.
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.True(f.AtHumanRate));
    }

    [Fact]
    public void Leaves_a_rule_above_its_human_rate_alone()
    {
        var findings = new[] { Hit("lex.a"), Hit("lex.a"), Hit("lex.a") };

        var result = GenreGate.Apply(findings, Pack(("lex.a", 2.0)), wordCount: 1000);

        Assert.All(result, f => Assert.False(f.AtHumanRate));
    }

    [Fact]
    public void A_rule_with_no_measured_rate_is_untouched()
    {
        // The strong tells — delve, tapestry — never appear in the human corpus, get no threshold,
        // and must keep counting on a single occurrence however long the document is.
        var findings = new[] { Hit("lex.delve") };

        var result = GenreGate.Apply(findings, Pack(("lex.other", 1.0)), wordCount: 100_000);

        Assert.Single(result);
        Assert.False(result[0].AtHumanRate);
    }

    [Fact]
    public void The_boundary_is_inclusive_so_exactly_the_human_rate_is_human()
    {
        var findings = new[] { Hit("lex.a") };

        var atExactly = GenreGate.Apply(findings, Pack(("lex.a", 1.0)), wordCount: 1000);
        var justOver = GenreGate.Apply(findings, Pack(("lex.a", 1.0)), wordCount: 999);

        Assert.True(atExactly[0].AtHumanRate);
        Assert.False(justOver[0].AtHumanRate);
    }

    [Fact]
    public void Rates_are_counted_per_rule_not_across_the_document()
    {
        var findings = new[] { Hit("lex.a"), Hit("lex.b"), Hit("lex.b"), Hit("lex.b") };

        var result = GenreGate.Apply(findings, Pack(("lex.a", 2.0), ("lex.b", 2.0)), wordCount: 1000);

        Assert.True(result.Single(f => f.RuleId == "lex.a").AtHumanRate);
        Assert.All(result.Where(f => f.RuleId == "lex.b"), f => Assert.False(f.AtHumanRate));
    }

    [Fact]
    public void Findings_at_a_human_rate_do_not_move_the_score()
    {
        // The whole point: an academic paper using "furthermore" the way academics use it should read
        // exactly as human as one that never uses it.
        var analyzer = new AiWritingAnalyzer();
        var body = string.Join(" ", Enumerable.Repeat("The study examined the data carefully and reported what it found.", 160));

        var withOne = analyzer.Analyze(body + " Furthermore, the result held.", "en");

        // Reported, so the reader still sees it…
        Assert.Contains(withOne.Findings, f => f.RuleId == "lex.furthermore");
        Assert.True(withOne.Findings.Single(f => f.RuleId == "lex.furthermore").AtHumanRate);

        // …and worth nothing, so it cannot push anybody toward an accusation. The lexical category is
        // asserted rather than the overall score on purpose: adding any sentence moves burstiness, and
        // a test that watched the overall number would be measuring sentence rhythm, not this gate.
        // The category tallies count evidence, not highlights: they exist to explain the score, so a
        // finding that scores nothing must not appear in them even though it is still in the panel.
        var lexical = withOne.CategoryScores.Single(c => c.Category == SignCategory.Lexical);
        Assert.Equal(0, lexical.FindingCount);
        Assert.Equal(0, lexical.Score);
    }

    [Fact]
    public void A_custom_catalog_that_reworks_a_rule_keeps_its_measured_rate()
    {
        // Rewording a suggestion is the documented way to contribute. Losing the rule's calibration
        // because the contributor had no reason to restate a number they never saw would be a trap.
        var contributed = new RulePack
        {
            Language = "en",
            Lexical = [new LexicalRule
            {
                Id = "lex.furthermore",
                Terms = ["furthermore"],
                Suggestion = "besides, also",
            }],
        };

        var merged = AiWritingAnalyzer.ResolvePack("en", [contributed]);

        Assert.True(merged.HumanRates.TryGetValue("lex.furthermore", out var rate));
        Assert.True(rate > 0);
    }

    [Fact]
    public void A_catalog_may_still_turn_a_rate_off_deliberately()
    {
        // Inheriting the built-in rate must not become impossible to override — only impossible to
        // lose by accident. A pack that means to disable it states zero, which is different from stating nothing.
        var contributed = new RulePack
        {
            Language = "en",
            Lexical = [new LexicalRule
            {
                Id = "lex.furthermore",
                Terms = ["furthermore"],
                Suggestion = "besides, also",
                HumanRatePer1000 = 0,
            }],
        };

        var merged = AiWritingAnalyzer.ResolvePack("en", [contributed]);

        Assert.False(merged.HumanRates.ContainsKey("lex.furthermore"));
    }

    [Fact]
    public void The_built_in_packs_carry_measured_rates()
    {
        // A regression guard for the packaging, not the analysis: these numbers live in JSON that is
        // rewritten by a tool, and losing them would quietly restore the seven-false-tells behaviour.
        Assert.NotEmpty(AiWritingAnalyzer.ResolvePack("en").HumanRates);
        Assert.NotEmpty(AiWritingAnalyzer.ResolvePack("es").HumanRates);
    }
}

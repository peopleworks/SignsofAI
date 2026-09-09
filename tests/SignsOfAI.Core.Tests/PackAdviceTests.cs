using SignsOfAI.Core.Rules;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The rule packs deliver their advice to a reader, and #82 noticed they delivered it with the very
/// mark one of the rules is about — including, with perfect irony, the three empty-intensifier rules.
///
/// Neither test below forbids an em-dash. The product measures density, and a prohibition would
/// contradict the thing it tells everyone else: keep them rare and deliberate. So the packs are held
/// to the rule's own threshold, and the second test guards the part of this that was never cosmetic.
/// </summary>
public class PackAdviceTests
{
    private static readonly string[] Languages = ["en", "es"];

    /// <summary>Every string a pack shows a reader, joined as the prose it effectively is.</summary>
    private static IEnumerable<string> ReaderFacing(RulePack pack) =>
        pack.Lexical.Select(r => r.Suggestion)
            .Concat(pack.Patterns.SelectMany(r => new[] { r.Message, r.Suggestion }))
            .Concat(pack.Messages?.Values ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s));

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void A_pack_keeps_its_own_advice_about_the_em_dash(string language)
    {
        var pack = RulePackLoader.Load(language);
        var prose = string.Join(' ', ReaderFacing(pack));

        var words = prose.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var dashes = prose.Count(c => c == '\u2014');
        var per100 = dashes / (double)words * 100.0;

        // The same 1.0 per 100 words EmDashAnalyzer applies to everyone. Held here rather than
        // asserted as zero, because a pack that may never use the mark is a stricter rule than the
        // one the product publishes, and the product would then be wrong about itself again.
        Assert.True(per100 < 1.0,
            $"rules.{language}.json advises at {per100:0.0} em-dashes per 100 words ({dashes} in "
            + $"{words}), which is the density this project's own rule calls high.");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void Advice_that_describes_a_problem_never_becomes_a_replacement(string language)
    {
        // This is the part of #82 that was not cosmetic. SuggestionParser cuts a suggestion at the
        // first aside marker, and the em-dash was one of them, so "empty intensifier — cut it"
        // yielded one term and was refused for being alone. Rewriting it with a comma would have
        // produced two terms and let the rewriter substitute the words "empty intensifier" into
        // somebody's sentence. A colon is filtered by the parser; a comma is not.
        var pack = RulePackLoader.Load(language);

        foreach (var rule in pack.Lexical.Where(r => r.Replacements is not { Length: > 0 }))
        {
            var salvaged = rule.RewriteOptions();

            Assert.True(salvaged.Count == 0,
                $"{rule.Id} states no replacements, yet its suggestion \"{rule.Suggestion}\" parses "
                + $"as [{string.Join(", ", salvaged)}] — which the live rewriter would substitute "
                + "into the writer's text. Describe the problem after a colon, not a comma.");
        }
    }
}

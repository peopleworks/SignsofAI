using SignsOfAI.Core;
using SignsOfAI.Core.Analyzers;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Guards the analyzer wording the packs now carry, in the spirit of the locale-file tests: a
/// translation is repository content people send as pull requests, so a mistake in one should be
/// reported by name here rather than discovered by a reader.
///
/// The risk this covers is specific. These templates take positional arguments, and a translator who
/// drops a <c>{0}</c> or invents a <c>{3}</c> produces a sentence with a hole in it — or, before the
/// guard in <see cref="RulePack.Text"/>, an exception in the middle of an analysis.
/// </summary>
public class PackMessageTests
{
    private static readonly string[] Languages = ["en", "es"];

    private static IEnumerable<int> Placeholders(string template)
    {
        for (var i = 0; i < 10; i++)
            if (template.Contains("{" + i + "}", StringComparison.Ordinal))
                yield return i;
    }

    [Fact]
    public void Every_builtin_pack_carries_every_message()
    {
        foreach (var lang in Languages)
        {
            var pack = RulePackLoader.Load(lang);
            Assert.NotNull(pack.Messages);

            foreach (var key in PackMessages.Arity.Keys)
                Assert.True(pack.Messages!.ContainsKey(key),
                    $"rules.{lang}.json is missing the message '{key}'.");
        }
    }

    [Fact]
    public void No_pack_carries_a_message_nobody_reads()
    {
        foreach (var lang in Languages)
        {
            var pack = RulePackLoader.Load(lang);
            foreach (var key in pack.Messages!.Keys)
                Assert.True(PackMessages.Arity.ContainsKey(key),
                    $"rules.{lang}.json defines '{key}', which no analyzer asks for. Typo?");
        }
    }

    [Fact]
    public void Every_message_uses_exactly_the_placeholders_it_is_given()
    {
        foreach (var lang in Languages)
        {
            var pack = RulePackLoader.Load(lang);
            foreach (var (key, arity) in PackMessages.Arity)
            {
                var template = pack.Messages![key];
                var used = Placeholders(template).ToArray();
                var expected = Enumerable.Range(0, arity).ToArray();

                Assert.True(used.SequenceEqual(expected),
                    $"rules.{lang}.json → '{key}' uses placeholders [{string.Join(",", used)}] " +
                    $"but is given {arity}. Expected [{string.Join(",", expected)}].");
            }
        }
    }

    [Fact]
    public void A_pack_without_messages_behaves_exactly_as_before()
    {
        // Every custom catalog people already have in their browser was written before `messages`
        // existed. None of them may change behaviour.
        var pack = new RulePack { Language = "*" };

        Assert.Equal(PackMessages.Defaults[PackMessages.BurstinessEvidence],
                     pack.Text(PackMessages.BurstinessEvidence));
        Assert.Equal("“delve” is heavily overused in AI writing.",
                     pack.Text(PackMessages.LexicalOverused, "delve"));
    }

    [Fact]
    public void The_downloadable_template_carries_the_messages()
    {
        // The Analyze page offers the English pack as a starting point for a custom catalog, built
        // with ToJson(). If serialization drops the block, everyone who starts from that template
        // gets a pack with no wording in it and no hint the field exists.
        var round = RulePack.FromJson(RulePackLoader.Load("en").ToJson());

        Assert.NotNull(round.Messages);
        foreach (var key in PackMessages.Arity.Keys)
            Assert.True(round.Messages!.ContainsKey(key), $"'{key}' did not survive the round trip.");
    }

    [Fact]
    public void A_broken_placeholder_costs_a_clumsy_sentence_not_an_exception()
    {
        var pack = new RulePack
        {
            Language = "*",
            Messages = new() { [PackMessages.LexicalOverused] = "«{0}» y luego un error {" },
        };

        // Returns the raw template rather than throwing mid-analysis.
        var text = pack.Text(PackMessages.LexicalOverused, "profundizar");
        Assert.Equal("«{0}» y luego un error {", text);
    }

    [Fact]
    public void Spanish_findings_are_written_in_Spanish()
    {
        // The reason this whole thing exists: analysing Spanish used to produce an English sentence
        // wrapped around a Spanish word. Goes through the public facade, so it checks what a reader
        // actually gets rather than what an analyzer returns in isolation.
        var text = "Además, cabe destacar que este enfoque además resulta interesante. " +
                   "Además, es fundamental profundizar en el vasto panorama de la innovación.";

        var result = new AiWritingAnalyzer().Analyze(text, "es");
        var lexical = result.Findings.Where(f => f.Category == SignCategory.Lexical).ToArray();

        Assert.NotEmpty(lexical);
        foreach (var f in lexical)
        {
            Assert.DoesNotContain("is heavily overused", f.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Consider:", f.Suggestion, StringComparison.Ordinal);
        }
    }
}

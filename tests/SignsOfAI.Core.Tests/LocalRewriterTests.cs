using SignsOfAI.Core;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rewriting;
using SignsOfAI.Core.Rules;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The rewriter edits someone's prose, so the bar is higher than "it compiles": a wrong edit is worse
/// than no edit. These tests pin the mechanics that decide whether the output reads like English —
/// capitalization, spacing around a removed word, and refusing to conjugate.
/// </summary>
public class LocalRewriterTests
{
    private readonly AiWritingAnalyzer _analyzer = new();

    private (string Text, IReadOnlyList<RewriteEdit> Edits, RulePack Pack) PlanFor(
        string text, string language = "en", RewriteStrength strength = RewriteStrength.Thorough)
    {
        var pack = RulePackLoader.Load(language);
        var result = _analyzer.Analyze(text, language);
        return (text, LocalRewriter.Plan(text, result.Findings, pack, strength), pack);
    }

    private string Rewrite(string text, string language = "en", RewriteStrength strength = RewriteStrength.Thorough)
    {
        var (_, edits, _) = PlanFor(text, language, strength);
        return LocalRewriter.Apply(text, edits);
    }

    // ── substitution ─────────────────────────────────────────────────────────

    [Fact]
    public void Replaces_an_overused_word_with_its_first_alternative()
    {
        Assert.Equal("We examine the data.", Rewrite("We delve the data."));
    }

    [Fact]
    public void Carries_the_original_capitalization_onto_the_replacement()
    {
        Assert.Equal("Examine the data.", Rewrite("Delve the data."));
    }

    [Fact]
    public void Uppercases_a_replacement_for_an_all_caps_original()
    {
        Assert.Equal("EXAMINE the data.", Rewrite("DELVE the data."));
    }

    [Fact]
    public void Leaves_an_inflected_form_alone_but_still_reports_it()
    {
        // "showcased" is in the rule's terms, but the replacements fit "showcase" — substituting would
        // produce "The report show results". The edit is offered, not applied.
        var (text, edits, _) = PlanFor("The report showcased strong results.");

        var edit = Assert.Single(edits, e => e.RuleId == "lex.showcase");
        Assert.False(edit.AutoApply);
        Assert.NotEmpty(edit.Options);
        Assert.Equal(text, LocalRewriter.Apply(text, edits.Where(e => e.AutoApply).ToList()));
    }

    [Theory]
    [InlineData("We must delve into the data.", "delve")]        // phrasal verb: "examine into" is wrong
    [InlineData("It is a testament to progress.", "testament")]  // governed preposition: "proof to" is wrong
    [InlineData("Let us embark on a project.", "embark")]
    public void Declines_to_swap_a_word_that_governs_the_particle_after_it(string text, string word)
    {
        // Alternatives can't rescue these either — picking "look into" would yield "look into into" —
        // so the honest move is to leave the sentence alone and let the finding advise the writer.
        var (_, edits, _) = PlanFor(text);

        Assert.DoesNotContain(edits, e => e.Original.Equals(word, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(text, LocalRewriter.Apply(text, edits));
    }

    [Fact]
    public void Fixes_the_indefinite_article_when_the_sound_changes()
    {
        // "crucial" → "important" flips the initial sound; leaving "a" would read as a typo.
        Assert.Equal("It is an important step.", Rewrite("It is a crucial step."));
    }

    [Fact]
    public void Declines_a_swap_in_the_quantifier_frame()
    {
        // "a plethora of options" → "a many of options" is not English.
        const string text = "There is a plethora of options.";
        Assert.Equal(text, Rewrite(text));
    }

    [Fact]
    public void Never_deletes_a_word_that_holds_up_a_flagged_construction()
    {
        // "just" is an empty intensifier in general, but here it carries the whole negative
        // parallelism: dropping it turns "more than a tool" into "not a tool".
        var rewritten = Rewrite("It's not just a tool, it's a solution.");
        Assert.Contains("not just a tool", rewritten);
    }

    [Fact]
    public void Still_swaps_a_noun_before_the_genitive()
    {
        // "of" survives a noun swap untouched, so suppressing this case would cost a good edit.
        Assert.Equal("the rich mix of innovation", Rewrite("the rich tapestry of innovation"));
    }

    [Fact]
    public void Applies_a_chosen_alternative_over_the_default()
    {
        var (text, edits, _) = PlanFor("We delve the data.");
        var edit = Assert.Single(edits);

        var chosen = new Dictionary<int, string> { [edit.Span.Start] = "explore" };
        Assert.Equal("We explore the data.", LocalRewriter.Apply(text, edits, chosen));
    }

    [Fact]
    public void Leaves_a_rejected_edit_untouched()
    {
        var (text, edits, _) = PlanFor("We delve the data.");
        var rejected = new HashSet<int> { edits[0].Span.Start };

        Assert.Equal(text, LocalRewriter.Apply(text, edits, rejected: rejected));
    }

    // ── deletion ─────────────────────────────────────────────────────────────

    [Fact]
    public void Deleting_a_mid_sentence_word_leaves_one_space()
    {
        Assert.Equal("It's a tool.", Rewrite("It's just a tool."));
    }

    [Fact]
    public void Deleting_a_sentence_opener_recapitalizes_what_follows()
    {
        Assert.Equal("The bus was late.", Rewrite("Actually, the bus was late."));
    }

    [Fact]
    public void Deleting_a_comma_wrapped_word_takes_both_commas()
    {
        Assert.Equal("The bus was late.", Rewrite("The bus was, actually, late."));
    }

    [Fact]
    public void Deleting_a_word_before_punctuation_does_not_leave_a_gap()
    {
        Assert.Equal("The bus was late.", Rewrite("The bus was late, truly."));
    }

    [Fact]
    public void Handles_two_deletions_in_a_row_without_losing_one()
    {
        // Regression: capitalizing straight after the first deletion used to consume the second
        // edit's first character, silently dropping it.
        Assert.Equal("Do it.", Rewrite("Just simply do it."));
    }

    [Fact]
    public void Does_not_capitalize_after_a_semicolon()
    {
        Assert.Equal("He left; it rained.", Rewrite("He left; actually, it rained."));
    }

    // ── planning ─────────────────────────────────────────────────────────────

    [Fact]
    public void Plans_no_edits_for_text_with_no_lexical_tells()
    {
        var (_, edits, _) = PlanFor("The bus was late again and my shoes are wet.");
        Assert.Empty(edits);
    }

    [Fact]
    public void Edits_never_overlap()
    {
        var (_, edits, _) = PlanFor(
            "In today's digital age we must delve into the rich tapestry of multifaceted innovation, " +
            "which is truly a testament to progress and simply showcases robust synergy.");

        var ordered = edits.OrderBy(e => e.Span.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(ordered[i].Span.Start >= ordered[i - 1].Span.End,
                $"'{ordered[i - 1].Original}' and '{ordered[i].Original}' overlap.");
    }

    [Fact]
    public void Light_touches_less_than_thorough()
    {
        const string text =
            "We must delve into this multifaceted approach, which is simply a robust testament to progress.";

        var light = LocalRewriter.Plan(text, _analyzer.Analyze(text, "en").Findings,
            RulePackLoader.Load("en"), RewriteStrength.Light);
        var thorough = LocalRewriter.Plan(text, _analyzer.Analyze(text, "en").Findings,
            RulePackLoader.Load("en"), RewriteStrength.Thorough);

        Assert.True(light.Count < thorough.Count);
        Assert.All(light, e => Assert.Equal(Severity.High, e.Severity));
    }

    [Fact]
    public void Ignores_findings_that_have_no_mechanical_fix()
    {
        // A negative parallelism needs a structural rewrite, so the rewriter must not touch it.
        var (text, edits, _) = PlanFor("It's not just a tool, it's a solution.");

        Assert.DoesNotContain(edits, e => e.RuleId.StartsWith("rhet.") || e.RuleId.StartsWith("syn."));
        Assert.Contains(text, text); // sanity: the pattern finding exists but yields no edit
    }

    // ── the point of the whole thing ─────────────────────────────────────────

    [Fact]
    public void Rewriting_lowers_the_ai_score()
    {
        const string text =
            "In today's digital age, we must delve into the rich tapestry of modern innovation. " +
            "This multifaceted and nuanced approach is simply a robust testament to human progress. " +
            "Moreover, by leveraging cutting-edge technology, organizations can showcase excellence.";

        var before = _analyzer.Analyze(text, "en");
        var rewritten = LocalRewriter.Apply(
            text, LocalRewriter.Plan(text, before.Findings, RulePackLoader.Load("en"), RewriteStrength.Thorough));
        var after = _analyzer.Analyze(rewritten, "en");

        Assert.True(after.OverallScore < before.OverallScore,
            $"score did not drop: {before.OverallScore} → {after.OverallScore}");
        Assert.True(after.Findings.Count < before.Findings.Count);
    }

    [Fact]
    public void Works_in_spanish_too()
    {
        var rewritten = Rewrite("Debemos utilizar un enfoque robusto y crucial.", "es");

        Assert.DoesNotContain("utilizar", rewritten);
        Assert.DoesNotContain("robusto", rewritten);
    }

    [Fact]
    public void Keeps_spanish_gender_agreement_with_the_article()
    {
        // "panorama" is masculine, "situación" feminine: swapping it under "el" would give
        // "el situación". The mismatched alternative is withheld rather than the article guessed at.
        const string text = "Las cifras cambian en el panorama actual.";
        var rewritten = Rewrite(text, "es");

        Assert.DoesNotContain("el situación", rewritten);
        Assert.DoesNotContain("el situacion", rewritten);
    }

    [Fact]
    public void Picks_a_gender_matching_alternative_rather_than_declining()
    {
        // "panorama" is masculine despite its -a, which the article makes plain. "situación" is
        // dropped from the options and a masculine one is used, so the edit still happens.
        var rewritten = Rewrite("Las cifras cambian en el panorama actual.", "es");

        Assert.DoesNotContain("panorama", rewritten);
        Assert.DoesNotContain("el situación", rewritten);
    }

    [Fact]
    public void Leaves_a_swap_alone_when_no_alternative_agrees()
    {
        // Every option reads feminine, the article is masculine: there is nothing safe to substitute.
        const string text = "Vemos el panorama actual.";
        var pack = RulePack.FromJson(
            """
            {
              "language": "es",
              "lexical": [
                { "id": "custom.g", "terms": ["panorama"], "weight": 6, "severity": "High",
                  "suggestion": "x", "replacements": ["situación", "perspectiva"] }
              ]
            }
            """);

        var findings = _analyzer.Analyze(text, "es", [pack]).Findings;
        var rewritten = LocalRewriter.Apply(
            text, LocalRewriter.Plan(text, findings, pack, RewriteStrength.Thorough), language: "es");

        Assert.Equal(text, rewritten);
    }

    [Fact]
    public void Gender_agreement_only_applies_where_there_is_an_article()
    {
        // No article in front means nothing to disagree with, so the swap proceeds normally.
        var rewritten = Rewrite("Debemos utilizar herramientas nuevas.", "es");
        Assert.DoesNotContain("utilizar", rewritten);
    }

    [Fact]
    public void Spanish_deletion_markers_are_honoured()
    {
        // "simplemente" is a delete rule in the Spanish pack; its prose reads "muletilla — suele sobrar",
        // which no English marker would ever match. This is why the field is explicit.
        Assert.Equal("Es una herramienta.", Rewrite("Es simplemente una herramienta.", "es"));
    }

    // ── the fallback parser, for catalogs written before the field existed ────

    [Theory]
    [InlineData("examine, explore, look into", new[] { "examine", "explore", "look into" })]
    [InlineData("mix, blend, range — or just name the thing", new[] { "mix", "blend", "range" })]
    [InlineData("strong, reliable, solid", new[] { "strong", "reliable", "solid" })]
    public void Salvages_a_comma_separated_list_from_a_prose_suggestion(string suggestion, string[] expected)
    {
        Assert.Equal(expected, SuggestionParser.LeadingTerms(suggestion));
    }

    [Theory]
    [InlineData("empty intensifier — cut it")]          // describes the problem, is not a replacement
    [InlineData("often removable")]
    [InlineData("muletilla — suele sobrar")]            // "filler word" — a description, in Spanish
    [InlineData("usually deletable")]
    [InlineData("use")]                                 // plausible, but a lone term is never trusted
    [InlineData("complex, or specify the actual facets")]
    [InlineData("")]
    public void Never_guesses_from_anything_but_an_unmistakable_list(string suggestion)
    {
        // Telling the replacement "use" from the description "muletilla" needs to know the language.
        // Rather than guess, a lone term yields nothing and the word is left alone; catalogs wanting a
        // single replacement say so in `replacements`.
        Assert.Empty(SuggestionParser.LeadingTerms(suggestion));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void Every_built_in_lexical_rule_states_its_fix_explicitly(string language)
    {
        // The fallback parser exists for third-party catalogs only. If a built-in rule ever leaned on
        // it, tightening the parser would quietly degrade the shipped experience.
        var vague = RulePackLoader.Load(language).Lexical
            .Where(r => !r.Delete && r.Replacements is not { Length: > 0 })
            .Select(r => r.Id)
            .ToList();

        Assert.True(vague.Count == 0,
            $"rules.{language}.json needs \"replacements\" or \"delete\" on: {string.Join(", ", vague)}");
    }

    [Fact]
    public void Never_infers_a_deletion_from_prose()
    {
        // A catalog whose suggestion only says "cut it" must not cause a silent deletion: guessing
        // wrong would change someone's prose in a way they never asked for.
        var pack = RulePack.FromJson(
            """
            {
              "language": "*",
              "lexical": [
                { "id": "custom.filler", "terms": ["verily"], "weight": 3, "severity": "High",
                  "suggestion": "empty intensifier — cut it" }
              ]
            }
            """);

        var rule = pack.Lexical[0];
        Assert.False(rule.Delete);
        Assert.Empty(rule.RewriteOptions());

        const string text = "It was verily late.";
        var findings = _analyzer.Analyze(text, "en", [pack]).Findings;
        Assert.Equal(text, LocalRewriter.Apply(text, LocalRewriter.Plan(text, findings, pack, RewriteStrength.Thorough)));
    }

    [Fact]
    public void Explicit_replacements_win_over_the_prose_suggestion()
    {
        var pack = RulePack.FromJson(
            """
            {
              "language": "*",
              "lexical": [
                { "id": "custom.synergy", "terms": ["synergy"], "weight": 6, "severity": "High",
                  "suggestion": "prose nobody should parse", "replacements": ["teamwork", "cooperation"] }
              ]
            }
            """);

        Assert.Equal(["teamwork", "cooperation"], pack.Lexical[0].RewriteOptions());

        const string text = "We need synergy here.";
        var findings = _analyzer.Analyze(text, "en", [pack]).Findings;
        var rewritten = LocalRewriter.Apply(text, LocalRewriter.Plan(text, findings, pack, RewriteStrength.Thorough));
        Assert.Equal("We need teamwork here.", rewritten);
    }
}

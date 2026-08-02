using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Shared vocabulary for these tests. Every character under test is written as an escape rather than
/// pasted in: a zero-width space in a source file is invisible to the next reader and survives a
/// careless editor only by luck, which is a poor foundation for the one check that claims to state
/// facts.
/// </summary>
internal static class Chars
{
    // Built from codepoints rather than pasted in. A zero-width space in a source file is invisible
    // to the next reader and survives a careless editor only by luck, which is a poor foundation for
    // the one check in the product that claims to state facts.
    private static string U(int codePoint) => char.ConvertFromUtf32(codePoint);

    public static readonly string ZeroWidthSpace = U(0x200B);
    public static readonly string ZeroWidthJoiner = U(0x200D);
    public static readonly string ZeroWidthNonJoiner = U(0x200C);
    public static readonly string NoBreakSpace = U(0x00A0);
    public static readonly string CyrillicA = U(0x0430);      // indistinguishable from "a"
    public static readonly string CyrillicE = U(0x0435);      // indistinguishable from "e"
    public static readonly string GreekAlpha = U(0x03B1);
    public static readonly string GreekBeta = U(0x03B2);
    public static readonly string MathBoldA = U(0x1D41A);     // MATHEMATICAL BOLD SMALL A
    public static readonly string TagLetterA = U(0xE0041);    // TAG LATIN CAPITAL LETTER A
    public static readonly string ManEmoji = U(0x1F468);
    public static readonly string WomanEmoji = U(0x1F469);
    public static readonly string PersianMi = U(0x0645) + U(0x06CC);
    public static readonly string PersianKhaham = U(0x062E) + U(0x0648) + U(0x0627) + U(0x0647) + U(0x0645);
}

/// <summary>
/// The character-artifact scan is the one check that returns a fact rather than a judgement, so it is
/// held to a different standard than the rest: a false positive here is not a debatable opinion about
/// prose, it is a wrong statement about what is in someone's file.
///
/// Most of what follows is therefore about what must *not* be flagged. Spanish accents, real Greek
/// words, emoji and Persian orthography all involve characters outside plain ASCII, and a scanner
/// that could not tell them from a substitution would be an instrument for punishing people over the
/// alphabet they write in.
/// </summary>
public class ArtifactScannerTests
{
    [Fact]
    public void Ordinary_text_yields_nothing()
    {
        var report = ArtifactScanner.Scan(
            "The bus was late again. I waited twelve minutes in the rain, and then it rolled past.");

        Assert.False(report.Any);
        Assert.Equal(ArtifactPattern.None, report.Pattern);
    }

    [Fact]
    public void Empty_and_null_input_are_handled()
    {
        Assert.False(ArtifactScanner.Scan(null).Any);
        Assert.False(ArtifactScanner.Scan(string.Empty).Any);
    }

    [Fact]
    public void Finds_an_invisible_character_and_says_exactly_where_it_is()
    {
        var text = $"Hello{Chars.ZeroWidthSpace}world";

        var report = ArtifactScanner.Scan(text);
        var found = Assert.Single(report.Occurrences);

        Assert.Equal(ArtifactKind.InvisibleCharacter, found.Kind);
        Assert.Equal("U+200B", found.CodePoint);
        Assert.Equal("ZERO WIDTH SPACE", found.CharacterName);
        Assert.True(found.IsStrong);

        // The position has to survive being handed to someone who will go and look.
        Assert.Equal(Chars.ZeroWidthSpace, found.Span.Slice(text));
        Assert.Equal(1, found.Line);
        Assert.Equal(6, found.Column);
    }

    [Fact]
    public void Reports_line_and_column_the_way_an_editor_counts_them()
    {
        var text = $"first line\nsecond line\nthird {Chars.ZeroWidthSpace}line";

        var found = Assert.Single(ArtifactScanner.Scan(text).Occurrences);

        Assert.Equal(3, found.Line);
        Assert.Equal(7, found.Column);
    }

    [Fact]
    public void Finds_a_letter_borrowed_from_another_script()
    {
        var text = $"This is an {Chars.CyrillicA}nalysis of the results.";

        var found = Assert.Single(ArtifactScanner.Scan(text).Occurrences);

        Assert.Equal(ArtifactKind.LookalikeLetter, found.Kind);
        Assert.Equal("U+0430", found.CodePoint);
        Assert.Equal("a", found.LooksLike);
        Assert.Equal($"{Chars.CyrillicA}nalysis", found.Word);
        Assert.True(found.IsStrong);
    }

    [Fact]
    public void Mathematical_letters_resolve_through_the_standard()
    {
        // Styled Latin folds via NFKC rather than a hand-written table, so the mapping is the
        // standard's rather than ours.
        var found = Assert.Single(ArtifactScanner.Scan($"an {Chars.MathBoldA}nalysis of it").Occurrences);

        Assert.Equal(ArtifactKind.LookalikeLetter, found.Kind);
        Assert.Equal("a", found.LooksLike);
        Assert.Equal(2, found.Span.Length); // a surrogate pair: the span must cover both code units
    }

    // ---- what must never be flagged --------------------------------------------------------------

    [Fact]
    public void Spanish_is_never_flagged_for_being_Spanish()
    {
        // If this test ever fails, the tool has become the thing it exists to argue against.
        var text = "El análisis de la señora Muñoz sobre la pingüinera fue rápido, límpido y útil. " +
                   "¿Qué más da? Añadió: «la investigación continúa». Él también lo dijo.";

        Assert.False(ArtifactScanner.Scan(text).Any);
    }

    [Fact]
    public void A_real_Greek_word_is_left_alone()
    {
        // "α-helix" is a Greek letter beside a Latin word, not one hiding inside it. The run of
        // letters it belongs to is entirely Greek, so there is nothing to claim.
        var text = $"The {Chars.GreekAlpha}-helix and the {Chars.GreekBeta}-sheet are the two motifs.";

        Assert.False(ArtifactScanner.Scan(text).Any);
    }

    [Fact]
    public void An_emoji_sequence_is_not_an_artifact()
    {
        // A zero-width joiner is how a multi-person emoji is built.
        var family = Chars.ManEmoji + Chars.ZeroWidthJoiner + Chars.WomanEmoji;

        Assert.False(ArtifactScanner.Scan($"We shipped it {family} today").Any);
    }

    [Fact]
    public void Join_controls_are_left_alone_in_the_scripts_that_need_them()
    {
        // Persian: the non-joiner is orthography, not tampering.
        var persian = Chars.PersianMi + Chars.ZeroWidthNonJoiner + Chars.PersianKhaham;

        Assert.False(ArtifactScanner.Scan(persian).Any);
    }

    [Fact]
    public void A_joiner_between_Latin_letters_is_an_artifact()
    {
        // The same character, with no orthographic reason to be there.
        var found = Assert.Single(ArtifactScanner.Scan($"dis{Chars.ZeroWidthJoiner}tance").Occurrences);

        Assert.Equal(ArtifactKind.InvisibleCharacter, found.Kind);
    }

    // ---- distribution ------------------------------------------------------------------------------

    private const string Filler =
        "Sentence number {0} carries enough words to give this document some length to measure. ";

    private static string Repeat(string template, int times) =>
        string.Concat(Enumerable.Range(0, times).Select(i => string.Format(template, i)));

    [Fact]
    public void A_handful_in_one_place_reads_as_incidental()
    {
        var text = $"Pasted{Chars.ZeroWidthSpace}from{Chars.ZeroWidthSpace}somewhere. " + Repeat(Filler, 20);

        var report = ArtifactScanner.Scan(text);

        Assert.Equal(ArtifactPattern.Incidental, report.Pattern);
        Assert.Contains("web page", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Artifacts_spread_through_the_document_read_as_systematic()
    {
        // What a tool that processed a whole text leaves behind: not many in one spot, but some
        // everywhere. That distribution is the measurement, and the summary states it.
        var text = Repeat($"Sentence number {{0}} carries{Chars.ZeroWidthSpace} enough words here. ", 20);

        var report = ArtifactScanner.Scan(text);

        Assert.Equal(ArtifactPattern.Systematic, report.Pattern);
        Assert.True(report.SectionsAffected * 2 >= report.SectionCount);
        Assert.Equal(20, report.StrongCount);
    }

    [Fact]
    public void Non_breaking_spaces_alone_never_read_as_systematic()
    {
        // Word inserts these on its own and a browser copy is full of them. They are reported, but
        // they are not allowed to carry a conclusion.
        var text = Repeat($"Sentence number {{0}} carries{Chars.NoBreakSpace}enough words here. ", 20);

        var report = ArtifactScanner.Scan(text);

        Assert.True(report.Any);
        Assert.Equal(0, report.StrongCount);
        Assert.Equal(ArtifactPattern.Incidental, report.Pattern);
    }

    [Fact]
    public void A_single_hidden_tag_character_is_enough()
    {
        // A tag character renders as nothing and exists to carry text a reader cannot see. There is
        // no innocent way for one to arrive in an essay, so spread is not required.
        var report = ArtifactScanner.Scan("An ordinary looking sentence." + Chars.TagLetterA);

        Assert.Equal(ArtifactPattern.Systematic, report.Pattern);
        Assert.Contains(report.Occurrences, o => o.Kind == ArtifactKind.TagCharacter);
    }

    [Fact]
    public void Identical_characters_are_rolled_up_for_reading()
    {
        var z = Chars.ZeroWidthSpace;
        var groups = ArtifactScanner.Scan($"a{z}b{z}c{z}d e").Groups;

        var zeroWidth = Assert.Single(groups, g => g.CodePoint == "U+200B");
        Assert.Equal(3, zeroWidth.Count);
        Assert.True(zeroWidth.IsStrong);
        Assert.True(groups[0].IsStrong); // strong kinds sort first: they are what carries meaning
    }

    // ---- wording -------------------------------------------------------------------------------------

    [Fact]
    public void Every_report_that_says_something_also_says_what_it_does_not_mean()
    {
        var report = ArtifactScanner.Scan($"Hello{Chars.ZeroWidthSpace}world");

        Assert.NotEmpty(report.Advice);
        Assert.Contains("not evidence", report.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_wording_comes_from_the_pack_so_Spanish_reads_in_Spanish()
    {
        var pack = Core.Rules.RulePackLoader.Load("es");

        var report = ArtifactScanner.Scan($"Hola{Chars.ZeroWidthSpace}mundo", pack);

        Assert.Contains("no ocupa espacio", report.Occurrences[0].Message, StringComparison.Ordinal);
        Assert.Contains("por dónde ha pasado el archivo", report.Advice, StringComparison.Ordinal);
    }
}

/// <summary>
/// Normalization is what stops a substituted letter from switching the catalog off, so these tests
/// are really about whether the detection rules survive contact with an adversary.
/// </summary>
public class TextNormalizerTests
{
    [Fact]
    public void Unchanged_text_is_passed_straight_through()
    {
        var normalized = TextNormalizer.Normalize("nothing unusual here");

        Assert.False(normalized.Changed);
        Assert.Equal("nothing unusual here", normalized.Text);
        Assert.Equal(7, normalized.ToSource(7));
    }

    [Fact]
    public void An_impostor_letter_becomes_the_letter_it_was_impersonating()
    {
        var normalized = TextNormalizer.Normalize($"we must d{Chars.CyrillicE}lve into it");

        Assert.True(normalized.Changed);
        Assert.Equal("we must delve into it", normalized.Text);
    }

    [Fact]
    public void Invisible_characters_are_removed_but_unusual_spaces_become_ordinary_ones()
    {
        // Deleting a space would weld two words into one token that matches nothing.
        Assert.Equal("wordword", TextNormalizer.Normalize($"word{Chars.ZeroWidthSpace}word").Text);
        Assert.Equal("word word", TextNormalizer.Normalize($"word{Chars.NoBreakSpace}word").Text);
    }

    [Fact]
    public void Positions_still_point_into_the_original_text()
    {
        var text = $"one{Chars.ZeroWidthSpace}two three";
        var normalized = TextNormalizer.Normalize(text);

        Assert.Equal("onetwo three", normalized.Text);

        // "three" starts at 7 in the cleaned copy and at 8 in the original.
        var span = normalized.ToSource(new TextSpan(7, 5));
        Assert.Equal("three", span.Slice(text));
    }

    [Fact]
    public void Nothing_is_cleaned_without_being_reported()
    {
        // The normalizer takes the report as its instructions rather than deciding for itself, so the
        // cleaned copy and the report can never describe different documents.
        var text = $"d{Chars.CyrillicE}lve{Chars.ZeroWidthSpace} deeper";

        var report = ArtifactScanner.Scan(text);
        var normalized = TextNormalizer.Apply(text, report);

        Assert.Equal("delve deeper", normalized.Text);
        Assert.Equal(2, report.Count);
    }
}

/// <summary>The attack, run end to end through the public entry point.</summary>
public class HomoglyphResistanceTests
{
    private static string Tampered(string word) =>
        word.Replace("e", Chars.CyrillicE, StringComparison.Ordinal);

    [Fact]
    public void A_substituted_letter_no_longer_switches_a_rule_off()
    {
        // Published work drives seven detectors below chance with exactly this substitution. The word
        // reads as "delve" on any screen, and before normalization the lexical rule saw a word it had
        // never heard of.
        var text = $"We must {Tampered("delve")} into the rich tapestry of the subject, " +
                   "because it is worth noting that the approach is multifaceted and nuanced.";

        var result = new AiWritingAnalyzer().Analyze(text, "en");

        var delve = Assert.Single(result.Findings, f => f.RuleId == "lex.delve");

        // And the finding points at the text the reader has, impostor letters and all — otherwise
        // they would go looking for a word that is not there.
        Assert.Equal(Tampered("delve"), delve.Span.Slice(text));
        Assert.Equal(Tampered("delve"), delve.MatchedText);
    }

    [Fact]
    public void The_substitution_is_reported_as_a_fact_of_its_own()
    {
        var text = $"We must {Tampered("delve")} into the subject at some length here.";

        var result = new AiWritingAnalyzer().Analyze(text, "en");

        Assert.Equal(2, result.Artifacts.Count);
        Assert.All(result.Artifacts.Occurrences, o => Assert.Equal(ArtifactKind.LookalikeLetter, o.Kind));
    }

    [Fact]
    public void Artifacts_never_move_the_score()
    {
        // The whole design rests on this. A score is arguable; a character at an offset is not, and
        // letting one feed the other would turn the fact back into an opinion.
        const string clean = "We must delve into the rich tapestry of this multifaceted subject, " +
                             "because it is worth noting that the approach is nuanced.";
        var tampered = clean.Replace("delve", Tampered("delve"), StringComparison.Ordinal);

        var analyzer = new AiWritingAnalyzer();
        var before = analyzer.Analyze(clean, "en");
        var after = analyzer.Analyze(tampered, "en");

        Assert.Equal(before.OverallScore, after.OverallScore);
        Assert.Equal(before.Findings.Count, after.Findings.Count);
        Assert.False(before.Artifacts.Any);
        Assert.True(after.Artifacts.Any);
    }

    [Fact]
    public void A_document_with_nothing_unusual_carries_an_empty_report()
    {
        var result = new AiWritingAnalyzer().Analyze("The bus was late again. I walked instead.", "en");

        Assert.False(result.Artifacts.Any);
        Assert.Equal(ArtifactPattern.None, result.Artifacts.Pattern);
    }
}

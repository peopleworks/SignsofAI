using SignsOfAI.Core;
using SignsOfAI.Core.Model;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The em-dash signal is a density of punctuation, so it must count punctuation and nothing else.
/// It used to count every "--" in the raw text, which charged a document for its own markup: a
/// command-line flag in a code block, a Markdown table separator, a frontmatter delimiter, or the
/// line of hyphens a student types above a bibliography. Every test here was checked by mutation —
/// each one fails against the old counter.
/// </summary>
public class EmDashCountingTests
{
    private readonly AiWritingAnalyzer _analyzer = new();

    // Long enough to clear the 40-word floor, with no dash of any kind in the prose.
    private const string PlainProse =
        "The bus was late again on Tuesday, and the shelter had no roof left on its east side. " +
        "I waited twelve minutes in the rain with a coffee going cold in my hand, then watched it " +
        "roll past the stop without slowing down. The driver did not look over. I walked the rest " +
        "of the way to the office and arrived before the next one was due, which tells you " +
        "something about the timetable and rather more about the traffic on that road.";

    private static Finding? EmDash(AnalysisResult result) =>
        result.Findings.FirstOrDefault(f => f.RuleId == "rhet.em-dash");

    [Fact]
    public void Command_line_flags_in_a_fenced_block_are_not_punctuation()
    {
        string text = PlainProse + "\n\n```bash\n" +
            "signsofai check draft.md --json\n" +
            "signsofai check essay.docx --report out.html\n" +
            "signsofai baseline a.docx --against b.docx --against c.docx\n" +
            "```\n";

        Assert.Null(EmDash(_analyzer.Analyze(text, "en")));
    }

    [Fact]
    public void A_flag_in_an_inline_code_span_is_not_punctuation()
    {
        string text = PlainProse + " Run it with `--json`, then with `--report out.html`, " +
                      "and compare what `--max-score 40` does to the exit code.";

        Assert.Null(EmDash(_analyzer.Analyze(text, "en")));
    }

    [Fact]
    public void A_markdown_table_separator_is_not_punctuation()
    {
        string text = PlainProse + "\n\n| Want | Tool | Runs |\n|---|---|---|\n" +
                      "| A score | the CLI | locally |\n|---|---|---|\n";

        Assert.Null(EmDash(_analyzer.Analyze(text, "en")));
    }

    [Fact]
    public void Frontmatter_delimiters_are_not_punctuation()
    {
        // Two delimiters alone stay under the three-dash floor; a section break makes three,
        // which is what a real document with frontmatter and a horizontal rule looks like.
        string text = "---\nname: signs-of-ai\ndescription: a skill\n---\n\n"
                      + PlainProse + "\n\n---\n\nAnd the section after the rule, which is also plain prose.";

        Assert.Null(EmDash(_analyzer.Analyze(text, "en")));
    }

    [Fact]
    public void A_line_of_hyphens_above_a_bibliography_is_not_punctuation()
    {
        // pelic-021970 in the calibration corpus: one separator line typed by a second-language
        // learner counted as twenty em-dashes and printed "LLMs lean on the em-dash" on their report.
        string text = PlainProse + "\n\n----------------------------------------\n" +
                      "1. Dr. Paul Jones, Stress and the Student, 2004.\n";

        Assert.Null(EmDash(_analyzer.Analyze(text, "en")));
    }

    [Fact]
    public void The_ascii_stand_in_still_counts_in_prose()
    {
        // Exactly two hyphens between words is how people type an em-dash without the key for it.
        string text = PlainProse.Replace(", and the shelter", " -- and the shelter")
                                .Replace(", then watched", " -- then watched")
                                .Replace(", which tells", " -- which tells");

        var finding = EmDash(_analyzer.Analyze(text, "en"));
        Assert.NotNull(finding);
    }

    [Fact]
    public void Real_em_dashes_still_count_in_prose()
    {
        string text = PlainProse.Replace(", and the shelter", " — and the shelter")
                                .Replace(", then watched", " — then watched")
                                .Replace(", which tells", " — which tells");

        Assert.NotNull(EmDash(_analyzer.Analyze(text, "en")));
    }

    [Fact]
    public void Markup_does_not_change_how_many_dashes_are_reported()
    {
        // Three real dashes in prose, plus a code block full of flags. The prose alone decides the
        // count. (The words inside the block still enter the denominator, which understates density
        // for technical documents — a separate defect, in the shared word count, not in this rule.)
        string prose = PlainProse.Replace(", and the shelter", " — and the shelter")
                                 .Replace(", then watched", " — then watched")
                                 .Replace(", which tells", " — which tells");

        var proseOnly = EmDash(_analyzer.Analyze(prose, "en"));
        var withMarkup = EmDash(_analyzer.Analyze(
            prose + "\n\n```bash\nsignsofai check a.md --json --lang en --top 5 --no-color\n```\n", "en"));

        Assert.NotNull(proseOnly);
        Assert.NotNull(withMarkup);
        Assert.Contains("(3 in ", proseOnly!.Message);
        Assert.Contains("(3 in ", withMarkup!.Message);
    }
}

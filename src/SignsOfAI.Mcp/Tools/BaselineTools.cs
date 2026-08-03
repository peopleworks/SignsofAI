using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core.Stylometry;

namespace SignsOfAI.Mcp.Tools;

/// <summary>
/// Compare a text against the same person's earlier work.
///
/// The tool description below carries more weight than usual. An agent holding this result is one
/// paraphrase away from telling somebody that a student cheated, and the measurement cannot support
/// that. So the description says what the numbers mean, says what they do not license, and names the
/// outcome that is actually most useful — the one where the text sits inside the writer's own range
/// and a suspicion is settled.
/// </summary>
[McpServerToolType]
public static class BaselineTools
{
    [McpServerTool(Name = "compare_to_baseline", ReadOnly = true),
     Description("""
        Compares one piece of writing against several earlier pieces by the SAME person, using function-word
        frequencies (Burrows's Delta). Returns how far the questioned text sits from that writer's centre,
        alongside how far each of the writer's own pieces sits from it — measured identically, so the scale is
        the writer's own variation rather than a threshold invented by this tool. Also returns which function
        words differ most, with rates per 1,000 words, and how many words are used at a rate the writer has
        never used them at. Runs fully offline; nothing is sent anywhere.
        WHAT THIS CANNOT DO: it cannot tell you who wrote something. There is no "different author" result and
        there must not be one in your summary either. Style moves with the assignment, the genre, the deadline,
        a co-author, an editor, and with a person simply getting better. A text outside the range is a reason
        to ask what changed; it is NEVER a conclusion, an accusation, or evidence of misconduct.
        The most valuable outcome is the reassuring one: a text INSIDE the range settles a suspicion, and
        saying so plainly is usually the most useful thing you can do with this tool.
        It refuses to answer on thin evidence and returns "Undetermined" instead of a number — do not work
        around that by rerunning with less text or by estimating one yourself.
        """)]
    public static BaselineComparison CompareToBaseline(
        [Description("Earlier pieces by the same writer. At least ~1,400 words in total across them.")] BaselineSample[] earlierWork,
        [Description("The piece being asked about. At least 300 words.")] string questionedText,
        [Description("Language: \"en\" or \"es\". Default \"en\".")] string language = "en",
        [Description("Optional title for the questioned piece.")] string questionedTitle = "questioned")
    {
        var pack = Core.Rules.RulePackLoader.Load(string.IsNullOrWhiteSpace(language) ? "en" : language);
        var samples = (earlierWork ?? [])
            .Select((s, i) => new AuthorSample($"s{i + 1}", s.Title ?? $"Sample {i + 1}", s.Text ?? string.Empty))
            .ToList();

        var report = StyleBaseline.Compare(
            samples, new AuthorSample("q", questionedTitle, questionedText ?? string.Empty), language, pack);

        return new BaselineComparison(
            report.Placement.ToString(),
            report.HasResult,
            report.Unavailable,
            report.Distance,
            report.WithinAuthorMax,
            report.WithinAuthorMedian,
            report.WithinAuthorDistances,
            report.WordsOutsideOwnRange,
            report.FeatureCount,
            report.BaselineWordCount,
            report.QuestionedWordCount,
            report.SampleCount,
            report.BaselineIsBroad,
            report.Summary,
            report.Advice,
            report.Drivers
                .Select(d => new WordDifference(d.Word, d.ZScore, d.QuestionedRate, d.BaselineRate,
                    d.BaselineLowest, d.BaselineHighest, d.UsedMore, d.OutsideOwnRange))
                .ToList());
    }
}

public sealed record BaselineSample(
    [property: Description("A label for this piece — an assignment name or a date.")] string? Title,
    [property: Description("The text of the piece.")] string? Text);

/// <param name="Placement">"WithinRange", "AtTheEdge", "BeyondRange", or "Undetermined" when there was not enough to measure. There is deliberately no value meaning "a different person wrote this".</param>
/// <param name="Distance">Meaningless on its own — read it against <paramref name="WithinAuthorMax"/>.</param>
/// <param name="WithinAuthorMax">The furthest any of the writer's own pieces sits from their centre, measured the same way.</param>
/// <param name="WordsOutsideOwnRange">How many measured words the questioned text uses at a rate the writer never uses. Reported as a fact; it does not decide the placement, because there is not enough evidence yet to put a cut point on it.</param>
/// <param name="BaselineIsBroad">The writer's own samples disagree a lot, which makes the whole comparison weak — often a sign one of them is not by the same person.</param>
public sealed record BaselineComparison(
    string Placement,
    bool HasResult,
    string? Unavailable,
    double Distance,
    double WithinAuthorMax,
    double WithinAuthorMedian,
    IReadOnlyList<double> WithinAuthorDistances,
    int WordsOutsideOwnRange,
    int WordsMeasured,
    int BaselineWordCount,
    int QuestionedWordCount,
    int SampleCount,
    bool BaselineIsBroad,
    string Summary,
    string Advice,
    IReadOnlyList<WordDifference> Drivers);

/// <param name="BaselineLowest">Lowest rate per 1,000 words across the writer's own pieces.</param>
/// <param name="OutsideOwnRange">The questioned rate falls outside everything the writer has done — checkable by counting.</param>
public sealed record WordDifference(
    string Word,
    double ZScore,
    double QuestionedRate,
    double BaselineRate,
    double BaselineLowest,
    double BaselineHighest,
    bool UsedMore,
    bool OutsideOwnRange);

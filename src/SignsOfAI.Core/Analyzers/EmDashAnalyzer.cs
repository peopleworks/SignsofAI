using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Analyzers;

/// <summary>
/// Flags over-reliance on the em-dash — an LLM's favorite rhythm crutch. A single em-dash is fine;
/// the tell is density. Emits one document-level finding (like burstiness) when em-dashes appear
/// far more often than human prose, which averages well under one per 100 words.
/// Counts real em-dashes (—), horizontal bars (―) and the "--" ASCII stand-in.
/// Counts them in prose only: a hyphen run inside a fenced block or a code span is a command-line
/// flag, and a run of three or more is a rule, a table separator or a frontmatter delimiter. None of
/// those is punctuation, and counting them charged technical documents for their own markup.
/// Categorised as Rhetorical so the signal counts toward the score, not just the report.
/// </summary>
public sealed class EmDashAnalyzer : IAnalyzer
{
    private const int MinWords = 40;       // too short to judge density
    private const int MinDashes = 3;       // a couple of dashes is normal
    private const double MinPer100 = 1.0;  // ~one em-dash per 100 words is already high for prose

    public SignCategory Category => SignCategory.Rhetorical;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        int words = context.Statistics.WordCount;
        if (words < MinWords)
            yield break;

        int dashes = CountEmDashes(context.Document.Raw);
        double per100 = dashes / (double)words * 100.0;
        if (dashes < MinDashes || per100 < MinPer100)
            yield break;

        var severity = per100 >= 3.0 ? Severity.High
            : per100 >= 1.6 ? Severity.Medium
            : Severity.Low;

        double weight = Math.Round(Math.Min(9.0, 2.0 + per100 * 2.0), 1);

        yield return new Finding
        {
            RuleId = "rhet.em-dash",
            Category = SignCategory.Rhetorical,
            Severity = severity,
            Span = new TextSpan(0, 0), // document-level
            MatchedText = string.Empty,
            Message = context.RulePack.Text(
                PackMessages.EmDashMessage, dashes, words, per100.ToString("0.0")),
            Suggestion = context.RulePack.Text(PackMessages.EmDashSuggestion),
            Evidence = context.RulePack.Text(PackMessages.EmDashEvidence),
            Weight = weight,
        };
    }

    /// <summary>
    /// Counts dashes used as punctuation. Skips fenced code blocks and inline code spans, where a
    /// "--" is a command-line flag; and skips hyphen runs of three or more, which are separators
    /// (---, |---|---|, a line of hyphens above a bibliography), never a dash between two clauses.
    /// The ASCII stand-in for an em-dash is exactly two hyphens.
    /// </summary>
    private static int CountEmDashes(string text)
    {
        int count = 0;
        bool insideFence = false;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                insideFence = !insideFence;
                continue;
            }

            if (!insideFence)
                count += CountInProseLine(line);
        }

        return count;
    }

    private static int CountInProseLine(string line)
    {
        int count = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '`')
            {
                // An inline code span holds flags and identifiers, not punctuation. An unmatched
                // backtick is just a character, so only skip when a closing one exists.
                int close = line.IndexOf('`', i + 1);
                if (close < 0)
                    continue;
                i = close;
            }
            else if (c is '—' or '―') // em dash, horizontal bar
            {
                count++;
            }
            else if (c == '-')
            {
                int run = 1;
                while (i + run < line.Length && line[i + run] == '-')
                    run++;

                if (run == 2)
                    count++; // the ASCII stand-in; a single hyphen joins words, three or more separate blocks

                i += run - 1;
            }
        }

        return count;
    }
}

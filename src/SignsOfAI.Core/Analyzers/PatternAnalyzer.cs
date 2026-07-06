using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Analyzers;

/// <summary>
/// Runs the rule-pack's regex patterns to catch multi-word rhetorical and syntactic tells:
/// negative parallelisms, false ranges, hedging transitions, copula avoidance, participial padding.
/// </summary>
public sealed class PatternAnalyzer : IAnalyzer
{
    // Compiled regexes are cached across analyses keyed by pattern text.
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    // Patterns span multiple categories; the rollup treats each finding by its own category.
    public SignCategory Category => SignCategory.Rhetorical;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        var text = context.Document.Raw;

        foreach (var rule in context.RulePack.Patterns)
        {
            var regex = RegexCache.GetOrAdd(rule.Regex, static p =>
                new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));

            foreach (Match m in regex.Matches(text))
            {
                if (m.Length == 0) continue;

                yield return new Finding
                {
                    RuleId = rule.Id,
                    Category = rule.Category,
                    Severity = rule.Severity,
                    Span = new TextSpan(m.Index, m.Length),
                    MatchedText = m.Value,
                    Message = rule.Message,
                    Suggestion = rule.Suggestion,
                    Evidence = rule.Evidence,
                    Weight = rule.Weight,
                };
            }
        }
    }
}

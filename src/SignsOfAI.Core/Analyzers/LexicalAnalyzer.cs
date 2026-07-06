using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Core.Analyzers;

/// <summary>
/// Flags overused AI vocabulary (delve, tapestry, nuanced…) by matching whole word tokens
/// against the rule-pack. Single-token matching keeps it precise; multi-word phrases live in
/// the pattern rules instead.
/// </summary>
public sealed class LexicalAnalyzer : IAnalyzer
{
    public SignCategory Category => SignCategory.Lexical;

    public IEnumerable<Finding> Analyze(AnalysisContext context)
    {
        // Build term -> rule lookup once per analysis.
        var lookup = new Dictionary<string, LexicalRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in context.RulePack.Lexical)
            foreach (var term in rule.Terms)
                lookup[term] = rule;

        foreach (var word in context.Document.Words)
        {
            if (!lookup.TryGetValue(word.Normalized, out var rule))
                continue;

            yield return new Finding
            {
                RuleId = rule.Id,
                Category = SignCategory.Lexical,
                Severity = rule.Severity,
                Span = word.Span,
                MatchedText = word.Text,
                Message = $"“{word.Text}” is heavily overused in AI writing.",
                Suggestion = $"Consider: {rule.Suggestion}",
                Evidence = rule.Evidence,
                Weight = rule.Weight,
            };
        }
    }
}

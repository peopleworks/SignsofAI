using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Core.Analyzers;

/// <summary>Everything an analyzer needs: the tokenized document, its language, rule-pack and stats.</summary>
public sealed class AnalysisContext
{
    public required TextDocument Document { get; init; }
    public required string Language { get; init; }
    public required RulePack RulePack { get; init; }
    public required TextStatistics Statistics { get; init; }
}

using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Analyzers;

/// <summary>A detector for one family of AI-writing signs. Stateless; safe to reuse.</summary>
public interface IAnalyzer
{
    SignCategory Category { get; }

    IEnumerable<Finding> Analyze(AnalysisContext context);
}

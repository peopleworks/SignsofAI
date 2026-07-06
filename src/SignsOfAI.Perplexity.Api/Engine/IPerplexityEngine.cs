namespace SignsOfAI.Perplexity.Api.Engine;

/// <summary>Raw output of a forward pass: the language-model uncertainty over the text.</summary>
/// <param name="Perplexity">exp(mean per-token NLL).</param>
/// <param name="MeanLogProb">Mean per-token log-probability (natural log; ≤ 0).</param>
/// <param name="ScoredTokens">Count of tokens whose probability was measured.</param>
/// <param name="ElapsedMs">Wall time inside the forward pass.</param>
public readonly record struct PerplexityRaw(double Perplexity, double MeanLogProb, int ScoredTokens, long ElapsedMs);

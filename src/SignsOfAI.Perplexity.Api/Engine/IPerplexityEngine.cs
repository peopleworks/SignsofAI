namespace SignsOfAI.Perplexity.Api.Engine;

/// <summary>Raw output of a forward pass: the language-model uncertainty over the text.</summary>
/// <param name="Perplexity">exp(mean per-token NLL).</param>
/// <param name="MeanLogProb">Mean per-token log-probability (natural log; ≤ 0).</param>
/// <param name="ScoredTokens">Count of tokens whose probability was measured.</param>
/// <param name="ElapsedMs">Wall time inside the forward pass.</param>
public readonly record struct PerplexityRaw(double Perplexity, double MeanLogProb, int ScoredTokens, long ElapsedMs);

/// <summary>
/// Computes causal-language-model perplexity for a piece of text. Implementations wrap an
/// ONNX model + tokenizer. Registered as a singleton — <see cref="ScoreAsync"/> must be safe
/// for concurrent calls.
/// </summary>
public interface IPerplexityEngine
{
    /// <summary>Short model identifier surfaced to clients (e.g. "qwen2.5-0.5b").</summary>
    string ModelId { get; }

    /// <summary>True once the model + tokenizer are loaded and ready to serve.</summary>
    bool IsReady { get; }

    /// <summary>Runs one forward pass and returns the perplexity of <paramref name="text"/>.</summary>
    Task<PerplexityRaw> ScoreAsync(string text, CancellationToken ct = default);
}

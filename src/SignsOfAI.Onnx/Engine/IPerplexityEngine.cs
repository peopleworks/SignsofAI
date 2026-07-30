namespace SignsOfAI.Onnx.Engine;

/// <summary>A reusable in-process causal-language-model engine.</summary>
public interface IPerplexityEngine
{
    /// <summary>Whether every local file required for inference is currently installed.</summary>
    bool IsAvailable { get; }

    /// <summary>Checks local model availability without loading ONNX Runtime or throwing for missing files.</summary>
    Task<bool> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs a forward pass over <paramref name="text"/>.</summary>
    Task<PerplexityRaw> ScoreAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// How a model download is going, for hosts that make a person wait for it.
///
/// A server fetches its weights at startup and logs the result; nobody is watching. An app where
/// someone pressed a button and is now waiting on several hundred megabytes needs to show that
/// something is happening, or the window looks hung.
/// </summary>
/// <param name="FileName">The file being fetched.</param>
/// <param name="BytesRead">Bytes written so far for this file.</param>
/// <param name="TotalBytes">Its total size, or null when the server sends no Content-Length.</param>
/// <param name="FileIndex">1-based position in the download.</param>
/// <param name="FileCount">How many files this model needs.</param>
public readonly record struct ModelDownloadProgress(
    string FileName, long BytesRead, long? TotalBytes, int FileIndex, int FileCount);

/// <summary>Raw output of a forward pass: the language-model uncertainty over the text.</summary>
/// <param name="Perplexity">exp(mean per-token NLL).</param>
/// <param name="MeanLogProb">Mean per-token log-probability (natural log; ≤ 0).</param>
/// <param name="ScoredTokens">Count of tokens whose probability was measured.</param>
/// <param name="ElapsedMs">Wall time inside the forward pass.</param>
public readonly record struct PerplexityRaw(double Perplexity, double MeanLogProb, int ScoredTokens, long ElapsedMs);

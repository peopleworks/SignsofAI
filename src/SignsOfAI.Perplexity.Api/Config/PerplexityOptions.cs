namespace SignsOfAI.Perplexity.Api.Config;

/// <summary>Per-language calibration of the perplexity → AI-likelihood mapping (measured on the chosen model).</summary>
public sealed class LangBaseline
{
    /// <summary>Decision boundary in natural-log perplexity: below ⇒ machine-leaning, above ⇒ human-leaning.</summary>
    public double BoundaryLogPpl { get; init; }

    /// <summary>Spread (≈ half the human↔AI gap) — the z-score denominator.</summary>
    public double Spread { get; init; } = 0.5;

    /// <summary>Logistic steepness mapping z-score → AI likelihood.</summary>
    public double Steepness { get; init; } = 1.4;
}

/// <summary>
/// Everything the perplexity service needs, bound from the "Perplexity" appsettings section so the
/// model, its tensor geometry, and the calibration are all tunable without a rebuild.
/// </summary>
public sealed class PerplexityOptions
{
    // ── Model + tokenizer files ──────────────────────────────────────────────
    /// <summary>Identifier surfaced to clients.</summary>
    public string ModelId { get; init; } = "qwen2.5-0.5b-int8";

    /// <summary>Directory holding the ONNX model + tokenizer.json (absolute, or relative to ContentRoot).</summary>
    public string ModelDir { get; init; } = "models/qwen2.5-0.5b";

    public string ModelFile { get; init; } = "model_int8.onnx";
    public string TokenizerFile { get; init; } = "tokenizer.json";

    /// <summary>If the model file is missing at startup and this is set, it is downloaded here once.</summary>
    public string? ModelUrl { get; init; }
    public string? TokenizerUrl { get; init; }

    // ── Tensor geometry (Qwen2.5-0.5B) ───────────────────────────────────────
    public int NumLayers { get; init; } = 24;
    public int NumKvHeads { get; init; } = 2;
    public int HeadDim { get; init; } = 64;
    public int Vocab { get; init; } = 151936;

    /// <summary>Right-truncate inputs to this many tokens for bounded latency.</summary>
    public int MaxTokens { get; init; } = 512;

    /// <summary>Intra-op thread count for the ONNX session (0 ⇒ ORT default).</summary>
    public int IntraOpThreads { get; init; } = 4;

    // ── Scoring ───────────────────────────────────────────────────────────────
    public Dictionary<string, LangBaseline> Baselines { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public double AiThreshold { get; init; } = 0.66;
    public double HumanThreshold { get; init; } = 0.34;

    /// <summary>
    /// Defaults calibrated locally on <c>model_int8.onnx</c> (2026-07-06): human EN log-ppl ≈ 4.6,
    /// AI EN ≈ 3.7 (boundary ≈ 4.1); human ES ≈ 4.5, AI ES ≈ 3.8. Refined with more samples over time.
    /// </summary>
    public static PerplexityOptions Defaults() => new()
    {
        Baselines =
        {
            ["en"] = new LangBaseline { BoundaryLogPpl = 4.10, Spread = 0.50, Steepness = 1.4 },
            ["es"] = new LangBaseline { BoundaryLogPpl = 4.10, Spread = 0.42, Steepness = 1.4 },
        },
    };
}

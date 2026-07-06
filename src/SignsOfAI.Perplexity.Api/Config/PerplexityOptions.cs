namespace SignsOfAI.Perplexity.Api.Config;

/// <summary>
/// Per-language calibration of the perplexity → <b>predictability</b> meter. Perplexity measures how
/// predictable/generic the phrasing is (NOT AI-vs-human: on a labeled corpus the two overlap badly —
/// memorized human text like Wikipedia scores <i>lower</i>/more-predictable than fresh AI text). So we
/// surface predictability honestly, centered per language (Spanish runs higher-perplexity than English).
/// </summary>
public sealed class LangBaseline
{
    /// <summary>Natural-log perplexity that maps to 50% predictability (the per-language corpus center).</summary>
    public double Center { get; init; }

    /// <summary>Spread (≈ 1 std of log-perplexity in this language) — the logistic denominator.</summary>
    public double Spread { get; init; } = 0.8;

    /// <summary>Logistic steepness mapping (center − logPpl)/spread → predictability.</summary>
    public double Steepness { get; init; } = 1.3;
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

    /// <summary>
    /// Free the model from RAM after this many seconds with no requests; the next request lazily
    /// reloads it from disk (~1-2s). ≤ 0 keeps it resident once loaded. Keeps the server light when idle.
    /// </summary>
    public int IdleUnloadSeconds { get; init; } = 300;

    /// <summary>Load the model into RAM at startup. Default false ⇒ lazy-load on first request (idle = ~0 model RAM).</summary>
    public bool PreloadModel { get; init; }

    // ── Predictability meter ────────────────────────────────────────────────────
    public Dictionary<string, LangBaseline> Baselines { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Predictability at/above which we call the phrasing "very-predictable" (generic).</summary>
    public double PredictableAbove { get; init; } = 0.60;

    /// <summary>Predictability at/below which we call the phrasing "varied" (surprising).</summary>
    public double VariedBelow { get; init; } = 0.40;

    /// <summary>
    /// Defaults calibrated on the instruct model over a 170-text EN/ES corpus (2026-07-07). Centers are
    /// the per-language corpus means of log-perplexity (EN ≈ 3.95, ES ≈ 4.18); ES runs higher.
    /// </summary>
    public static PerplexityOptions Defaults() => new()
    {
        Baselines =
        {
            ["en"] = new LangBaseline { Center = 4.35, Spread = 0.75, Steepness = 1.3 },
            ["es"] = new LangBaseline { Center = 4.55, Spread = 0.75, Steepness = 1.3 },
        },
    };
}

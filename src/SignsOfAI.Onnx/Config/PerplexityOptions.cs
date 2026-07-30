namespace SignsOfAI.Onnx.Config;

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
/// A single selectable model: its ONNX files, tensor geometry, and per-language predictability calibration.
/// The engine is model-agnostic — everything model-specific lives here so new models are pure config.
/// </summary>
public sealed class ModelProfile
{
    /// <summary>Stable id used in requests + surfaced to clients (e.g. "qwen2.5-0.5b-instruct-int8").</summary>
    public string Id { get; init; } = "";

    /// <summary>Short UI label (e.g. "Qwen 0.5B").</summary>
    public string Label { get; init; } = "";

    /// <summary>One-line note for the picker (vendor / size / speed).</summary>
    public string Note { get; init; } = "";

    // ── Files ────────────────────────────────────────────────────────────────
    public string ModelDir { get; init; } = "";
    public string ModelFile { get; init; } = "model.onnx";
    public string TokenizerFile { get; init; } = "tokenizer.json";
    public string? ModelUrl { get; init; }
    public string? TokenizerUrl { get; init; }

    /// <summary>Extra files to fetch into ModelDir by basename — e.g. ONNX external-data (*.onnx_data).</summary>
    public string[] AuxFileUrls { get; init; } = [];

    // ── Tensor geometry ──────────────────────────────────────────────────────
    public int NumLayers { get; init; }
    public int NumKvHeads { get; init; }
    public int HeadDim { get; init; }
    public int Vocab { get; init; }
    public int MaxTokens { get; init; } = 512;
    public int IntraOpThreads { get; init; } = 4;

    // ── Predictability calibration ───────────────────────────────────────────
    public Dictionary<string, LangBaseline> Baselines { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public double PredictableAbove { get; init; } = 0.60;
    public double VariedBelow { get; init; } = 0.40;

    public LangBaseline Baseline(string lang)
    {
        var key = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant();
        if (key is "auto" or "") key = "en";
        if (key.Length > 2) key = key[..2];
        if (Baselines.TryGetValue(key, out var b)) return b;
        if (Baselines.TryGetValue("en", out var en)) return en;
        return new LangBaseline { Center = 4.2 };
    }

    internal IEnumerable<string> ValidationErrors()
    {
        if (string.IsNullOrWhiteSpace(Id)) yield return "Model Id is required.";
        if (string.IsNullOrWhiteSpace(ModelDir)) yield return $"Model '{Id}' requires ModelDir.";
        if (string.IsNullOrWhiteSpace(ModelFile)) yield return $"Model '{Id}' requires ModelFile.";
        if (string.IsNullOrWhiteSpace(TokenizerFile)) yield return $"Model '{Id}' requires TokenizerFile.";
        if (NumLayers <= 0) yield return $"Model '{Id}' requires NumLayers > 0.";
        if (NumKvHeads <= 0) yield return $"Model '{Id}' requires NumKvHeads > 0.";
        if (HeadDim <= 0) yield return $"Model '{Id}' requires HeadDim > 0.";
        if (Vocab <= 0) yield return $"Model '{Id}' requires Vocab > 0.";
        if (MaxTokens < 2) yield return $"Model '{Id}' requires MaxTokens >= 2.";
        if (Baselines.Count == 0) yield return $"Model '{Id}' requires at least one language baseline.";
        foreach (var (lang, baseline) in Baselines)
        {
            if (string.IsNullOrWhiteSpace(lang)) yield return $"Model '{Id}' has an empty baseline language.";
            if (baseline.Spread <= 0) yield return $"Model '{Id}' baseline '{lang}' requires Spread > 0.";
            if (baseline.Steepness <= 0) yield return $"Model '{Id}' baseline '{lang}' requires Steepness > 0.";
        }
        if (VariedBelow < 0 || VariedBelow > 1) yield return $"Model '{Id}' requires VariedBelow in [0, 1].";
        if (PredictableAbove < 0 || PredictableAbove > 1) yield return $"Model '{Id}' requires PredictableAbove in [0, 1].";
        if (VariedBelow > PredictableAbove)
            yield return $"Model '{Id}' requires VariedBelow <= PredictableAbove.";
    }
}

/// <summary>
/// Service-wide config, bound from the "Perplexity" appsettings section. Holds the selectable models and
/// the shared idle-memory policy so the model, geometry, and calibration are all tunable without a rebuild.
/// </summary>
public sealed class PerplexityOptions
{
    /// <summary>Selectable models. The first is the default unless <see cref="DefaultModel"/> is set.</summary>
    public List<ModelProfile> Models { get; init; } = [];

    /// <summary>Id of the default model (used when a request omits "model"). Defaults to the first model.</summary>
    public string? DefaultModel { get; init; }

    /// <summary>
    /// Free a model from RAM after this many seconds with no requests to it; the next request lazily
    /// reloads it from disk. ≤ 0 keeps it resident once loaded. Keeps the server light when idle.
    /// </summary>
    public int IdleUnloadSeconds { get; init; } = 300;

    /// <summary>Load the default model into RAM at startup. Default false ⇒ lazy-load on first request.</summary>
    public bool PreloadModel { get; init; }

    /// <summary>Returns configuration errors without reading files or loading a model.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Models.Count == 0)
        {
            errors.Add("At least one perplexity model must be configured.");
            return errors;
        }

        errors.AddRange(Models.SelectMany(model => model.ValidationErrors()));
        var duplicateIds = Models
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        errors.AddRange(duplicateIds.Select(id => $"Duplicate model Id '{id}'."));

        if (!string.IsNullOrWhiteSpace(DefaultModel) &&
            !Models.Any(model => string.Equals(model.Id, DefaultModel, StringComparison.OrdinalIgnoreCase)))
            errors.Add($"Default model '{DefaultModel}' is not configured.");

        return errors;
    }

    /// <summary>Defaults: Qwen2.5-0.5B-Instruct (fast default) + Phi-4-mini (Microsoft, heavier, optional).</summary>
    public static PerplexityOptions Defaults() => new()
    {
        DefaultModel = "qwen2.5-0.5b-instruct-int8",
        Models =
        {
            new ModelProfile
            {
                Id = "qwen2.5-0.5b-instruct-int8", Label = "Qwen 0.5B", Note = "Alibaba, fast",
                ModelDir = "models/qwen2.5-0.5b-instruct", ModelFile = "model_int8.onnx",
                ModelUrl = "https://huggingface.co/onnx-community/Qwen2.5-0.5B-Instruct/resolve/main/onnx/model_int8.onnx",
                TokenizerUrl = "https://huggingface.co/onnx-community/Qwen2.5-0.5B-Instruct/resolve/main/tokenizer.json",
                NumLayers = 24, NumKvHeads = 2, HeadDim = 64, Vocab = 151936,
                Baselines =
                {
                    ["en"] = new LangBaseline { Center = 4.35, Spread = 0.75, Steepness = 1.3 },
                    ["es"] = new LangBaseline { Center = 4.55, Spread = 0.75, Steepness = 1.3 },
                },
            },
            new ModelProfile
            {
                Id = "phi-4-mini-q4", Label = "Phi-4-mini", Note = "Microsoft, 3.8B",
                ModelDir = "models/phi-4-mini", ModelFile = "model_q4.onnx",
                NumLayers = 32, NumKvHeads = 8, HeadDim = 128, Vocab = 200064, IntraOpThreads = 4,
                AuxFileUrls =
                [
                    "https://huggingface.co/onnx-community/Phi-4-mini-instruct-ONNX/resolve/main/onnx/model_q4.onnx_data",
                    "https://huggingface.co/onnx-community/Phi-4-mini-instruct-ONNX/resolve/main/onnx/model_q4.onnx_data_1",
                ],
                ModelUrl = "https://huggingface.co/onnx-community/Phi-4-mini-instruct-ONNX/resolve/main/onnx/model_q4.onnx",
                TokenizerUrl = "https://huggingface.co/onnx-community/Phi-4-mini-instruct-ONNX/resolve/main/tokenizer.json",
                Baselines =
                {
                    ["en"] = new LangBaseline { Center = 3.65, Spread = 0.72, Steepness = 1.3 },
                    ["es"] = new LangBaseline { Center = 3.75, Spread = 0.72, Steepness = 1.3 },
                },
            },
        },
    };
}

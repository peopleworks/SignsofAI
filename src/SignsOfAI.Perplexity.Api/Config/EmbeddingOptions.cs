namespace SignsOfAI.Perplexity.Api.Config;

/// <summary>
/// A single selectable sentence-embedding model (for the paraphrase / semantic-similarity check).
/// The engine is model-agnostic; everything model-specific lives here so new models are pure config.
/// EmbeddingGemma expects a task prompt prefix and supports Matryoshka truncation of its 768-dim output.
/// </summary>
public sealed class EmbeddingProfile
{
    /// <summary>Stable id used in requests + surfaced to clients (e.g. "embeddinggemma-300m-int8").</summary>
    public string Id { get; init; } = "";

    /// <summary>Short UI label (e.g. "EmbeddingGemma").</summary>
    public string Label { get; init; } = "";

    /// <summary>One-line note for the UI (vendor / size).</summary>
    public string Note { get; init; } = "";

    // ── Files ────────────────────────────────────────────────────────────────
    public string ModelDir { get; init; } = "";
    public string ModelFile { get; init; } = "model_quantized.onnx";
    public string TokenizerFile { get; init; } = "tokenizer.json";
    public string? ModelUrl { get; init; }
    public string? TokenizerUrl { get; init; }

    /// <summary>Extra files to fetch into ModelDir by basename — e.g. ONNX external-data (*.onnx_data).</summary>
    public string[] AuxFileUrls { get; init; } = [];

    // ── Inference ──────────────────────────────────────────────────────────────
    public int MaxTokens { get; init; } = 256;
    public int IntraOpThreads { get; init; } = 4;

    /// <summary>Native embedding width the model emits (EmbeddingGemma = 768).</summary>
    public int OutputDim { get; init; } = 768;

    /// <summary>Matryoshka truncation used when a request doesn't specify one (lighter payloads, strong quality).</summary>
    public int DefaultDims { get; init; } = 256;

    /// <summary>Task prompt prepended to every text. For symmetric similarity EmbeddingGemma wants the STS prompt.</summary>
    public string QueryPrefix { get; init; } = "task: sentence similarity | query: ";
}

/// <summary>
/// Config for the optional embedding subsystem, bound from the "Embedding" appsettings section. Mirrors the
/// perplexity side's lazy-load + idle-unload policy so the 300M model only occupies RAM while it's in use.
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>Turn the whole embedding feature off without removing config.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Selectable embedding models. The first is the default unless <see cref="DefaultModel"/> is set.</summary>
    public List<EmbeddingProfile> Models { get; init; } = [];

    public string? DefaultModel { get; init; }

    /// <summary>Free the model from RAM after this many idle seconds; the next request lazily reloads it.</summary>
    public int IdleUnloadSeconds { get; init; } = 300;

    /// <summary>Load the default model into RAM at startup. Default false ⇒ lazy-load on first request.</summary>
    public bool PreloadModel { get; init; }

    /// <summary>Default: Google EmbeddingGemma-300M (int8 ONNX, multilingual, on-device sized).</summary>
    public static EmbeddingOptions Defaults() => new()
    {
        DefaultModel = "embeddinggemma-300m-int8",
        Models =
        {
            new EmbeddingProfile
            {
                Id = "embeddinggemma-300m-int8", Label = "EmbeddingGemma", Note = "Google, 300M, multilingual",
                ModelDir = "models/embeddinggemma-300m", ModelFile = "model_quantized.onnx",
                ModelUrl = "https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX/resolve/main/onnx/model_quantized.onnx",
                TokenizerUrl = "https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX/resolve/main/tokenizer.json",
                AuxFileUrls =
                [
                    "https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX/resolve/main/onnx/model_quantized.onnx_data",
                ],
                OutputDim = 768, DefaultDims = 256, MaxTokens = 256,
            },
        },
    };
}

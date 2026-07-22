using System.Text.Json.Serialization;

namespace SignsOfAI.Perplexity.Api.Model;

/// <summary>Request body for POST /api/perplexity.</summary>
public sealed record PerplexityRequest
{
    /// <summary>The text to score. Required, non-empty.</summary>
    public string Text { get; init; } = "";

    /// <summary>"en", "es", or "auto" (default). Selects the calibration baseline.</summary>
    public string Lang { get; init; } = "auto";

    /// <summary>Which model to score with (id from GET /). Null/blank/unknown ⇒ the default model.</summary>
    public string? Model { get; init; }
}

/// <summary>
/// Response body for POST /api/perplexity. This reports how <b>predictable</b> the phrasing is — NOT an
/// AI-vs-human verdict (perplexity can't separate them reliably: memorized human text scores predictable
/// too, and stylized AI scores varied). Predictable phrasing is common in AI writing, so it's a useful
/// signal, but it is not proof.
/// </summary>
public sealed record PerplexityResponse
{
    /// <summary>Perplexity = exp(mean per-token negative log-likelihood). Lower ⇒ more predictable/generic.</summary>
    public double Ppl { get; init; }

    /// <summary>Mean per-token log-probability (natural log). Higher (closer to 0) ⇒ more predictable.</summary>
    public double AvgLogProb { get; init; }

    /// <summary>Number of tokens actually scored (the model saw one more; the first isn't predicted).</summary>
    public int TokenCount { get; init; }

    /// <summary>How predictable/generic the phrasing is, 0..1 (1 = very predictable, common in AI writing).</summary>
    public double Predictability { get; init; }

    /// <summary>"very-predictable" | "typical" | "varied".</summary>
    public string Band { get; init; } = "typical";

    /// <summary>Identifier of the model that produced the score (e.g. "qwen2.5-0.5b-instruct-int8").</summary>
    public string Model { get; init; } = "";

    /// <summary>The language calibration actually used ("en"/"es").</summary>
    public string Lang { get; init; } = "en";

    /// <summary>Milliseconds spent inside the model forward pass (diagnostics).</summary>
    public long ElapsedMs { get; init; }
}

/// <summary>One selectable model, surfaced to clients for the model picker.</summary>
public sealed record ModelInfo
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Note { get; init; } = "";
    public bool IsDefault { get; init; }
    /// <summary>Currently resident in RAM (false while idle-unloaded or not yet used).</summary>
    public bool Loaded { get; init; }
}

/// <summary>Health/metadata payload for GET /.</summary>
public sealed record ServiceInfo
{
    public string Service { get; init; } = "SignsOfAI.Perplexity.Api";
    /// <summary>The default model can serve (its file is present).</summary>
    public bool ModelReady { get; init; }
    public string[] Languages { get; init; } = [];
    /// <summary>All selectable perplexity models.</summary>
    public ModelInfo[] Models { get; init; } = [];
    /// <summary>The default embedding model can serve (its file is present).</summary>
    public bool EmbeddingReady { get; init; }
    /// <summary>Selectable embedding models (for the paraphrase check). Empty when the feature is off.</summary>
    public ModelInfo[] EmbeddingModels { get; init; } = [];
    /// <summary>The operator has configured an automatic web search (Phase D+). Off by default.</summary>
    public bool WebSearchReady { get; init; }
}

/// <summary>One web page returned for a searched phrase.</summary>
public sealed record WebHit
{
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public string Snippet { get; init; } = "";
    /// <summary>The returned snippet visibly contains the phrase (strong evidence, not just a loose match).</summary>
    public bool Verbatim { get; init; }
}

/// <summary>The web hits found for one distinctive phrase.</summary>
public sealed record PhraseHits
{
    public string Phrase { get; init; } = "";
    public WebHit[] Hits { get; init; } = [];
}

/// <summary>Request body for POST /api/webcheck.</summary>
public sealed record WebCheckRequest
{
    /// <summary>Distinctive phrases to look up on the web (exact-phrase). Required, non-empty.</summary>
    public string[] Phrases { get; init; } = [];
}

/// <summary>Response body for POST /api/webcheck.</summary>
public sealed record WebCheckResponse
{
    public PhraseHits[] Results { get; init; } = [];
    public long ElapsedMs { get; init; }
}

/// <summary>Request body for POST /api/embed — the paraphrase/semantic-similarity embedding endpoint.</summary>
public sealed record EmbedRequest
{
    /// <summary>Texts (typically sentences) to embed. Required, non-empty.</summary>
    public string[] Texts { get; init; } = [];

    /// <summary>Which embedding model to use (id from GET /). Null/blank/unknown ⇒ the default.</summary>
    public string? Model { get; init; }

    /// <summary>Matryoshka output width (e.g. 128/256/512/768). Null ⇒ the model's default.</summary>
    public int? Dims { get; init; }
}

/// <summary>Response body for POST /api/embed. Vectors are L2-normalized, so cosine similarity == dot product.</summary>
public sealed record EmbedResponse
{
    public string Model { get; init; } = "";
    public int Dims { get; init; }
    public float[][] Vectors { get; init; } = [];
    public long ElapsedMs { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PerplexityRequest))]
[JsonSerializable(typeof(PerplexityResponse))]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(EmbedRequest))]
[JsonSerializable(typeof(EmbedResponse))]
[JsonSerializable(typeof(float[][]))]
[JsonSerializable(typeof(WebCheckRequest))]
[JsonSerializable(typeof(WebCheckResponse))]
public partial class ApiJsonContext : JsonSerializerContext;

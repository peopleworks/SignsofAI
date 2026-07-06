using System.Text.Json.Serialization;

namespace SignsOfAI.Perplexity.Api.Model;

/// <summary>Request body for POST /api/perplexity.</summary>
public sealed record PerplexityRequest
{
    /// <summary>The text to score. Required, non-empty.</summary>
    public string Text { get; init; } = "";

    /// <summary>"en", "es", or "auto" (default). Selects the calibration baseline.</summary>
    public string Lang { get; init; } = "auto";
}

/// <summary>Response body for POST /api/perplexity.</summary>
public sealed record PerplexityResponse
{
    /// <summary>Perplexity = exp(mean per-token negative log-likelihood). Lower ⇒ more machine-like.</summary>
    public double Ppl { get; init; }

    /// <summary>Mean per-token log-probability (natural log). Higher (closer to 0) ⇒ more predictable.</summary>
    public double AvgLogProb { get; init; }

    /// <summary>Number of tokens actually scored (the model saw one more; the first isn't predicted).</summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// Standardized distance below the human baseline for this language:
    /// (humanLogPpl − logPpl) / spread. Positive ⇒ less surprising than typical human prose ⇒ AI-leaning.
    /// </summary>
    public double ZScore { get; init; }

    /// <summary>Mapped probability the text is AI-generated, 0..1 (logistic over ZScore).</summary>
    public double AiLikelihood { get; init; }

    /// <summary>"likely-ai" | "uncertain" | "likely-human".</summary>
    public string Verdict { get; init; } = "uncertain";

    /// <summary>Identifier of the model that produced the score (e.g. "qwen2.5-0.5b").</summary>
    public string Model { get; init; } = "";

    /// <summary>The language baseline actually used ("en"/"es").</summary>
    public string Lang { get; init; } = "en";

    /// <summary>Milliseconds spent inside the model forward pass (diagnostics).</summary>
    public long ElapsedMs { get; init; }
}

/// <summary>Health/metadata payload for GET /.</summary>
public sealed record ServiceInfo
{
    public string Service { get; init; } = "SignsOfAI.Perplexity.Api";
    public string Model { get; init; } = "";
    public bool ModelReady { get; init; }
    public string[] Languages { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PerplexityRequest))]
[JsonSerializable(typeof(PerplexityResponse))]
[JsonSerializable(typeof(ServiceInfo))]
public partial class ApiJsonContext : JsonSerializerContext;

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

/// <summary>Health/metadata payload for GET /.</summary>
public sealed record ServiceInfo
{
    public string Service { get; init; } = "SignsOfAI.Perplexity.Api";
    public string Model { get; init; } = "";
    /// <summary>The service can serve requests (model file present; may lazy-load on demand).</summary>
    public bool ModelReady { get; init; }
    /// <summary>The model is currently resident in RAM (false while idle-unloaded).</summary>
    public bool ModelLoaded { get; init; }
    public string[] Languages { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PerplexityRequest))]
[JsonSerializable(typeof(PerplexityResponse))]
[JsonSerializable(typeof(ServiceInfo))]
public partial class ApiJsonContext : JsonSerializerContext;

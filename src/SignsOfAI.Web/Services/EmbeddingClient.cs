using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Web.Services;

/// <summary>Request body for POST /api/embed.</summary>
public sealed record EmbedRequestDto(string[] Texts, string? Model = null, int? Dims = null);

/// <summary>Response body for POST /api/embed. Vectors are L2-normalized (cosine == dot product).</summary>
public sealed record EmbedResponseDto
{
    public string Model { get; init; } = "";
    public int Dims { get; init; }
    public float[][] Vectors { get; init; } = [];
    public long ElapsedMs { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EmbedRequestDto))]
[JsonSerializable(typeof(EmbedResponseDto))]
[JsonSerializable(typeof(float[][]))]
public partial class EmbeddingJsonContext : JsonSerializerContext;

/// <summary>
/// Calls the optional server-side embedding endpoint used by the paraphrase check. Like the perplexity
/// client, this is one of the features that sends text off the device — strictly opt-in, and the UI
/// discloses it before any call.
/// </summary>
public sealed class EmbeddingClient(HttpClient http)
{
    /// <summary>Embeds each text into an L2-normalized vector. Throws with a friendly message on failure.</summary>
    public async Task<float[][]> EmbedAsync(string endpoint, string[] texts, string? model = null, int? dims = null, CancellationToken ct = default)
    {
        var url = endpoint.TrimEnd('/') + "/api/embed";
        var req = new EmbedRequestDto(texts, model, dims);

        using var resp = await http.PostAsJsonAsync(url, req, EmbeddingJsonContext.Default.EmbedRequestDto, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("The embedding model is loading on the server — it downloads on first use and can take a minute or two. Try again shortly.");
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("The paraphrase check isn't enabled on this server.");
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Server returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");

        var result = await resp.Content.ReadFromJsonAsync(EmbeddingJsonContext.Default.EmbedResponseDto, ct);
        return result?.Vectors ?? throw new InvalidOperationException("Empty response from the embedding server.");
    }

    /// <summary>Fire-and-forget pre-warm so the embedding model is resident before the user runs the check.</summary>
    public async Task WarmupAsync(string endpoint, string? model = null, CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(model) ? "" : "?model=" + Uri.EscapeDataString(model);
        try { using var _ = await http.GetAsync(endpoint.TrimEnd('/') + "/api/embed/warmup" + q, ct); }
        catch { /* best-effort */ }
    }
}

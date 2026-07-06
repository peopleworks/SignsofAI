using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Web.Services;

/// <summary>Request/response DTOs for the SignsOfAI perplexity server (see SignsOfAI.Perplexity.Api).</summary>
public sealed record PerplexityRequest(string Text, string Lang);

public sealed record PerplexityResult
{
    public double Ppl { get; init; }
    public double AvgLogProb { get; init; }
    public int TokenCount { get; init; }
    /// <summary>How predictable/generic the phrasing is, 0..1 (1 = very predictable, common in AI writing).</summary>
    public double Predictability { get; init; }
    /// <summary>"very-predictable" | "typical" | "varied".</summary>
    public string Band { get; init; } = "typical";
    public string Model { get; init; } = "";
    public string Lang { get; init; } = "";
    public long ElapsedMs { get; init; }
}

/// <summary>User setting for the optional perplexity server (persisted in localStorage).</summary>
public sealed record PerplexitySettings
{
    /// <summary>The PeopleWorks-hosted perplexity endpoint, pre-filled so the feature works out of the box.</summary>
    public const string DefaultEndpoint = "https://signsofai.perplexity.api.peopleworksservices.com";

    /// <summary>Base URL of the perplexity API (empty = feature off). Defaults to the hosted endpoint.</summary>
    public string Endpoint { get; init; } = DefaultEndpoint;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PerplexityRequest))]
[JsonSerializable(typeof(PerplexityResult))]
[JsonSerializable(typeof(PerplexitySettings))]
public partial class PerplexityJsonContext : JsonSerializerContext;

/// <summary>
/// Calls the optional server-side perplexity endpoint. This is the ONE feature that sends text off
/// the device — it is strictly opt-in, and the UI discloses it. Everything else runs in the browser.
/// </summary>
public sealed class PerplexityClient(HttpClient http)
{
    private const string StorageKey = "signsofai.perplexity.v1";

    public async Task<PerplexitySettings> LoadSettingsAsync(BrowserStorage storage)
    {
        var json = await storage.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return new PerplexitySettings();
        try { return JsonSerializer.Deserialize(json, PerplexityJsonContext.Default.PerplexitySettings) ?? new(); }
        catch { return new PerplexitySettings(); }
    }

    public async Task SaveSettingsAsync(BrowserStorage storage, PerplexitySettings settings) =>
        await storage.SetAsync(StorageKey, JsonSerializer.Serialize(settings, PerplexityJsonContext.Default.PerplexitySettings));

    /// <summary>Measures perplexity for <paramref name="text"/>. Throws with a friendly message on failure.</summary>
    public async Task<PerplexityResult> MeasureAsync(string endpoint, string text, string lang, CancellationToken ct = default)
    {
        var url = endpoint.TrimEnd('/') + "/api/perplexity";
        var req = new PerplexityRequest(text, string.IsNullOrWhiteSpace(lang) ? "auto" : lang);

        using var resp = await http.PostAsJsonAsync(url, req, PerplexityJsonContext.Default.PerplexityRequest, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("The model is warming up on the server — try again in a few seconds.");
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Server returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");

        var result = await resp.Content.ReadFromJsonAsync(PerplexityJsonContext.Default.PerplexityResult, ct);
        return result ?? throw new InvalidOperationException("Empty response from the perplexity server.");
    }
}

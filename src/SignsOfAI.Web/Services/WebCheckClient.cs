using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Web.Services;

/// <summary>One web page returned for a searched phrase.</summary>
public sealed record WebHitDto
{
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
    public string Snippet { get; init; } = "";
    public bool Verbatim { get; init; }
}

/// <summary>The web hits found for one distinctive phrase.</summary>
public sealed record PhraseHitsDto
{
    public string Phrase { get; init; } = "";
    public WebHitDto[] Hits { get; init; } = [];
}

public sealed record WebCheckResponseDto
{
    public PhraseHitsDto[] Results { get; init; } = [];
    public long ElapsedMs { get; init; }
}

public sealed record WebCheckRequestDto(string[] Phrases);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WebCheckRequestDto))]
[JsonSerializable(typeof(WebCheckResponseDto))]
public partial class WebCheckJsonContext : JsonSerializerContext;

/// <summary>
/// Calls the optional server-side automatic web spot-check. This only exists when the operator configured a
/// search provider (see the server's WebSearch options); otherwise the UI stays on the on-device one-click
/// searches. Returns null when the feature isn't enabled so callers can fall back gracefully.
/// </summary>
public sealed class WebCheckClient(HttpClient http)
{
    /// <summary>Searches the web for each phrase. Returns null if the server doesn't offer the feature.</summary>
    public async Task<PhraseHitsDto[]?> CheckAsync(string endpoint, string[] phrases, CancellationToken ct = default)
    {
        var url = endpoint.TrimEnd('/') + "/api/webcheck";
        using var resp = await http.PostAsJsonAsync(url, new WebCheckRequestDto(phrases), WebCheckJsonContext.Default.WebCheckRequestDto, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // feature not enabled
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Web search returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");
        var result = await resp.Content.ReadFromJsonAsync(WebCheckJsonContext.Default.WebCheckResponseDto, ct);
        return result?.Results ?? [];
    }
}

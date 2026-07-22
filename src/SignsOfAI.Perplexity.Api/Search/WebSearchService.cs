using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using SignsOfAI.Perplexity.Api.Config;
using SignsOfAI.Perplexity.Api.Model;

namespace SignsOfAI.Perplexity.Api.Search;

/// <summary>A search backend that finds web pages for an exact phrase.</summary>
public interface IWebSearchProvider
{
    Task<IReadOnlyList<WebHit>> SearchAsync(string phrase, int count, CancellationToken ct);
}

/// <summary>
/// Orchestrates the optional automatic web spot-check: caps how much we search (quota safety for live
/// demos), caches per-phrase results in memory so re-runs don't burn quota, and verifies whether each
/// returned snippet actually contains the phrase so we only badge genuine verbatim matches. Entirely
/// inert unless an operator configured a provider + key (see <see cref="WebSearchOptions"/>).
/// </summary>
public sealed class WebSearchService
{
    private readonly WebSearchOptions _options;
    private readonly IWebSearchProvider? _provider;
    private readonly ConcurrentDictionary<string, IReadOnlyList<WebHit>> _cache = new();

    public WebSearchService(WebSearchOptions options, ILogger<WebSearchService> log)
    {
        _options = options;
        if (!options.IsActive) return;
        var key = options.ResolveKey()!;
        _provider = options.Provider.ToLowerInvariant() switch
        {
            "brave" => new BraveSearchProvider(key, log),
            _ => new BraveSearchProvider(key, log), // default provider; abstracted for others
        };
    }

    public bool IsActive => _provider is not null;

    public async Task<IReadOnlyList<PhraseHits>> CheckAsync(IReadOnlyList<string> phrases, CancellationToken ct)
    {
        if (_provider is null) return [];
        var results = new List<PhraseHits>();
        foreach (var raw in phrases.Take(_options.MaxPhrasesPerDoc))
        {
            var phrase = (raw ?? "").Trim();
            if (phrase.Length < 8) continue; // too short to be a meaningful fingerprint
            ct.ThrowIfCancellationRequested();

            if (!_cache.TryGetValue(phrase, out var hits))
            {
                var found = await _provider.SearchAsync(phrase, _options.MaxResultsPerPhrase, ct);
                hits = found.Select(h => h with { Verbatim = SnippetContains(h, phrase) }).ToArray();
                _cache[phrase] = hits;
            }
            results.Add(new PhraseHits { Phrase = phrase, Hits = [.. hits] });
        }
        return results;
    }

    /// <summary>True when the hit's snippet/title visibly contains the phrase (accent/case-insensitive).</summary>
    private static bool SnippetContains(WebHit hit, string phrase)
    {
        var hay = Fold(hit.Snippet + " " + hit.Title);
        return hay.Contains(Fold(phrase), StringComparison.Ordinal);
    }

    internal static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.IsWhiteSpace(c) ? ' ' : c);
        // collapse runs of spaces
        var collapsed = new StringBuilder(sb.Length);
        bool prevSpace = false;
        foreach (var c in sb.ToString())
        {
            if (c == ' ') { if (!prevSpace) collapsed.Append(' '); prevSpace = true; }
            else { collapsed.Append(c); prevSpace = false; }
        }
        return collapsed.ToString().Trim();
    }
}

/// <summary>Brave Search API provider. Queries the exact (quoted) phrase and returns the web results.</summary>
public sealed class BraveSearchProvider : IWebSearchProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly string _apiKey;
    private readonly ILogger _log;

    public BraveSearchProvider(string apiKey, ILogger log) { _apiKey = apiKey; _log = log; }

    public async Task<IReadOnlyList<WebHit>> SearchAsync(string phrase, int count, CancellationToken ct)
    {
        try
        {
            var q = Uri.EscapeDataString("\"" + phrase + "\"");
            var url = $"https://api.search.brave.com/res/v1/web/search?q={q}&count={Math.Clamp(count, 1, 20)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("X-Subscription-Token", _apiKey);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Brave search returned {Status} for a phrase query.", (int)resp.StatusCode);
                return [];
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("web", out var web) ||
                !web.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return [];

            var hits = new List<WebHit>();
            foreach (var r in results.EnumerateArray())
            {
                var u = Str(r, "url");
                if (string.IsNullOrWhiteSpace(u)) continue;
                hits.Add(new WebHit
                {
                    Url = u,
                    Title = StripHtml(Str(r, "title")),
                    Snippet = StripHtml(Str(r, "description")),
                });
                if (hits.Count >= count) break;
            }
            return hits;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Brave search failed; falling back to no results.");
            return [];
        }
    }

    private static string Str(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string StripHtml(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
        var sb = new StringBuilder(s.Length);
        bool inTag = false;
        foreach (var c in s)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}

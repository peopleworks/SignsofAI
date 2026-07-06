using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Web.Services;

/// <summary>A user-supplied catalog, persisted in the browser as its rule-pack JSON.</summary>
public sealed class CustomCatalog
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; set; } = "*";
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("ruleCount")] public int RuleCount { get; set; }
    [JsonPropertyName("json")] public string Json { get; set; } = string.Empty;
}

/// <summary>
/// Manages the user's own catalogs ("bring your own style guide"). Catalogs live only in the
/// browser's localStorage and are merged on top of the built-in EN/ES packs at analysis time.
/// </summary>
public sealed partial class CatalogStore(BrowserStorage storage)
{
    private const string StorageKey = "signsofai.catalogs.v1";

    public List<CustomCatalog> Catalogs { get; private set; } = [];
    private IReadOnlyList<RulePack> _enabledCache = [];

    public async Task LoadAsync()
    {
        var raw = await storage.GetAsync(StorageKey);
        if (!string.IsNullOrEmpty(raw))
        {
            try { Catalogs = JsonSerializer.Deserialize(raw, CatalogJsonContext.Default.ListCustomCatalog) ?? []; }
            catch { Catalogs = []; }
        }
        RebuildCache();
    }

    /// <summary>The parsed rule-packs for the enabled catalogs (cached; rebuilt on change).</summary>
    public IReadOnlyList<RulePack> EnabledPacks() => _enabledCache;

    /// <summary>Quick-add: turn a list of words/phrases into a lexical catalog.</summary>
    public async Task AddBannedWordsAsync(string name, string wordsRaw, string language)
    {
        var words = SplitRegex().Split(wordsRaw)
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (words.Count == 0) return;

        var slug = Slug(name);
        var pack = new RulePack
        {
            Language = string.IsNullOrWhiteSpace(language) ? "*" : language,
            Lexical = words.Select((w, i) => new LexicalRule
            {
                Id = $"custom.{slug}.{i}",
                Terms = [w],
                Weight = 4.0,
                Severity = Severity.Medium,
                Suggestion = "rephrase or remove — flagged by your catalog",
            }).ToArray(),
        };
        Add(name, pack.Language, pack.ToJson(), words.Count);
        await SaveAsync();
    }

    /// <summary>Advanced: import a full rule-pack JSON. Throws if the JSON is invalid.</summary>
    public async Task AddJsonAsync(string name, string json, string language)
    {
        var pack = RulePack.FromJson(json); // validates
        int count = (pack.Lexical?.Length ?? 0) + (pack.Patterns?.Length ?? 0);
        var lang = string.IsNullOrWhiteSpace(language) ? (string.IsNullOrWhiteSpace(pack.Language) ? "*" : pack.Language) : language;
        Add(name, lang, json, count);
        await SaveAsync();
    }

    public async Task ToggleAsync(CustomCatalog c) { c.Enabled = !c.Enabled; await SaveAsync(); }
    public async Task RemoveAsync(CustomCatalog c) { Catalogs.Remove(c); await SaveAsync(); }

    private void Add(string name, string language, string json, int ruleCount) =>
        Catalogs.Add(new CustomCatalog
        {
            Name = string.IsNullOrWhiteSpace(name) ? "My catalog" : name.Trim(),
            Language = language,
            Json = json,
            RuleCount = ruleCount,
            Enabled = true,
        });

    private async Task SaveAsync()
    {
        RebuildCache();
        await storage.SetAsync(StorageKey, JsonSerializer.Serialize(Catalogs, CatalogJsonContext.Default.ListCustomCatalog));
    }

    private void RebuildCache()
    {
        var packs = new List<RulePack>();
        foreach (var c in Catalogs.Where(c => c.Enabled))
        {
            try { packs.Add(RulePack.FromJson(c.Json)); } catch { /* skip a corrupt catalog */ }
        }
        _enabledCache = packs;
    }

    private static string Slug(string s)
    {
        var slug = SlugRegex().Replace(s.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "cat" : slug;
    }

    [GeneratedRegex(@"[,\r\n;]+")] private static partial Regex SplitRegex();
    [GeneratedRegex(@"[^a-z0-9]+")] private static partial Regex SlugRegex();
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<CustomCatalog>))]
internal partial class CatalogJsonContext : JsonSerializerContext;

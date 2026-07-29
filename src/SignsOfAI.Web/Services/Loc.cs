using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Web.Services;

/// <summary>One entry in <c>wwwroot/i18n/locales.json</c>.</summary>
public sealed class LocaleInfo
{
    /// <summary>Short language code — the file is <c>wwwroot/i18n/{code}.json</c>.</summary>
    [JsonPropertyName("code")] public string Code { get; set; } = "";

    /// <summary>English name, for maintainers reading the manifest.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>The language's name *in that language* — what a speaker of it looks for in the UI.</summary>
    [JsonPropertyName("endonym")] public string Endonym { get; set; } = "";

    /// <summary>Who contributed the translation. Shown on the switch so contributors get credit.</summary>
    [JsonPropertyName("credit")] public string Credit { get; set; } = "";
}

public sealed class LocaleManifest
{
    [JsonPropertyName("fallback")] public string Fallback { get; set; } = "en";
    [JsonPropertyName("locales")] public LocaleInfo[] Locales { get; set; } = [];
}

/// <summary>
/// The UI language (interface chrome), independent from the language of the text being analyzed.
///
/// Translations are plain JSON under <c>wwwroot/i18n/</c>, listed in <c>locales.json</c> — the same
/// "it's just a data file" approach the rule-packs already use. Adding a language means adding one
/// file and one manifest line: no C#, no recompile of any logic, no build step. Missing keys fall
/// back to the manifest's fallback locale, so a half-finished translation is still shippable and
/// useful rather than a wall of blanks.
///
/// Deliberately not <c>IStringLocalizer</c> + satellite .resx assemblies: those need a compiler to
/// produce, which is exactly the barrier we don't want in front of a translator, and switching
/// culture that way forces a page reload. Here the flip is instant.
///
/// Components re-render on change by deriving from <see cref="Components.LocalizedComponent"/>.
/// </summary>
public sealed class Loc(HttpClient http, IJSRuntime js, BrowserStorage storage)
{
    private const string StorageKey = "signsofai.ui.lang";
    private const string BasePath = "i18n";

    private readonly Dictionary<string, Dictionary<string, string>> _loaded = new(StringComparer.OrdinalIgnoreCase);

    private LocaleInfo[] _locales = [];
    private string _fallbackCode = "en";
    private string _lang = "en";
    private Dictionary<string, string> _strings = new();
    private Dictionary<string, string> _fallback = new();

    /// <summary>Every language offered by the manifest, in manifest order.</summary>
    public IReadOnlyList<LocaleInfo> Locales => _locales;

    /// <summary>The active language code.</summary>
    public string Current => _lang;

    public LocaleInfo? CurrentLocale =>
        _locales.FirstOrDefault(l => string.Equals(l.Code, _lang, StringComparison.OrdinalIgnoreCase));

    /// <summary>Raised after the language changes so subscribed components can re-render.</summary>
    public event Action? Changed;

    /// <summary>
    /// A translated string. Falls through to the fallback locale, then to the key itself — so an
    /// untranslated entry shows readable English rather than an empty gap.
    /// </summary>
    public string this[string key] =>
        _strings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v
        : _fallback.TryGetValue(key, out var f) && !string.IsNullOrWhiteSpace(f) ? f
        : key;

    /// <summary>A string with <c>{0}</c>-style placeholders filled in.</summary>
    public string F(string key, params object?[] args) => Format(this[key], args);

    /// <summary>
    /// A string containing inline markup (<c>&lt;strong&gt;</c>, <c>&lt;code&gt;</c>, links).
    ///
    /// Locale files are repository content reviewed in a pull request, not user input, so rendering
    /// them as markup is the same trust level as the .razor files themselves. Reviewers of a new
    /// translation should read it as code: see Docs/TRANSLATING.md.
    /// </summary>
    public MarkupString M(string key) => new(this[key]);

    /// <summary>Markup with placeholders filled in.</summary>
    public MarkupString M(string key, params object?[] args) => new(Format(this[key], args));

    /// <summary>
    /// Count-aware lookup: reads <c>{key}.one</c> or <c>{key}.other</c> and formats <paramref name="n"/>
    /// into it. English and Spanish agree on the 1-vs-rest split; a language needing more forms can
    /// phrase its strings to work with both slots.
    /// </summary>
    public string P(int n, string key) => F(n == 1 ? key + ".one" : key + ".other", n);

    /// <summary>
    /// A bad placeholder in a community-contributed string shouldn't blank out a whole page, so a
    /// malformed format falls back to the raw text instead of throwing.
    /// </summary>
    private static string Format(string template, object?[] args)
    {
        try { return string.Format(template, args); }
        catch (FormatException) { return template; }
    }

    // ── loading ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the manifest, picks the language (stored preference → browser language → fallback) and
    /// loads its strings. Called once from <c>Program.cs</c> before the first render, so a Spanish
    /// visitor never sees a flash of English chrome.
    /// </summary>
    public async Task InitAsync()
    {
        var manifest = await GetJsonAsync($"{BasePath}/locales.json", LocJsonContext.Default.LocaleManifest);
        if (manifest is { Locales.Length: > 0 })
        {
            _locales = manifest.Locales;
            _fallbackCode = Known(manifest.Fallback) ?? _locales[0].Code;
        }
        else
        {
            // No manifest: still offer English so the switch renders something sane.
            _locales = [new LocaleInfo { Code = "en", Name = "English", Endonym = "English" }];
            _fallbackCode = "en";
        }

        _fallback = await LoadStringsAsync(_fallbackCode);

        var stored = Known(await storage.GetAsync(StorageKey));
        var chosen = stored ?? Known(await BrowserPreferenceAsync()) ?? _fallbackCode;

        _lang = chosen;
        _strings = chosen == _fallbackCode ? _fallback : await LoadStringsAsync(chosen);
        await SyncHtmlLangAsync();
    }

    public async Task SetAsync(string code)
    {
        var next = Known(code);
        if (next is null || next == _lang) return;

        _strings = next == _fallbackCode ? _fallback : await LoadStringsAsync(next);
        _lang = next;
        await storage.SetAsync(StorageKey, next);
        await SyncHtmlLangAsync();
        Changed?.Invoke();
    }

    private async Task<Dictionary<string, string>> LoadStringsAsync(string code)
    {
        if (_loaded.TryGetValue(code, out var cached)) return cached;

        var loaded = await GetJsonAsync($"{BasePath}/{code}.json", LocJsonContext.Default.DictionaryStringString)
                     ?? new Dictionary<string, string>();
        _loaded[code] = loaded;
        return loaded;
    }

    private async Task<T?> GetJsonAsync<T>(string path, JsonTypeInfo<T> typeInfo) where T : class
    {
        try
        {
            // Read as a string first: a mis-deployed path returns index.html with a 200, and that
            // would otherwise surface as a confusing JSON error.
            using var response = await http.GetAsync(path);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (Exception ex)
        {
            await LogAsync($"[i18n] could not load {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The code from the manifest matching <paramref name="raw"/>, or null if unknown.</summary>
    private string? Known(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var wanted = raw.Trim();

        var exact = _locales.FirstOrDefault(l => string.Equals(l.Code, wanted, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact.Code;

        // "es-DO", "pt-BR" → match on the primary subtag so regional variants land somewhere useful.
        var primary = wanted.Split('-')[0];
        return _locales.FirstOrDefault(l => string.Equals(l.Code, primary, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    private async Task<string?> BrowserPreferenceAsync()
    {
        try { return await js.InvokeAsync<string?>("signsofai.browserLang"); }
        catch { return null; }
    }

    /// <summary>Keeps <c>&lt;html lang&gt;</c> honest — screen readers and translation tools read it.</summary>
    private async Task SyncHtmlLangAsync()
    {
        try { await js.InvokeVoidAsync("signsofai.setHtmlLang", _lang); }
        catch { /* cosmetic only — never worth failing startup over */ }
    }

    private async Task LogAsync(string message)
    {
        try { await js.InvokeVoidAsync("console.warn", message); }
        catch { /* nothing useful left to do */ }
    }

    // ── labels for Core enums / score bands ──────────────────────────────────
    // These live here rather than in Core so the engine stays language-neutral: Core returns a score
    // and a category, the UI decides what to call them in the reader's language.

    public string Category(SignCategory category) => this["cat." + category.ToString().ToLowerInvariant()];

    public string Sev(Severity severity) => this["sev." + severity.ToString().ToLowerInvariant()];

    /// <summary>The one-line verdict for an overall score. Mirrors the bands in <c>AnalysisResult.Verdict</c>.</summary>
    public string Verdict(double score) => score switch
    {
        >= 70 => this["verdict.strong"],
        >= 45 => this["verdict.moderate"],
        >= 20 => this["verdict.light"],
        _ => this["verdict.minimal"],
    };

    /// <summary>How to name the language the analyzer settled on. The engine only knows EN and ES.</summary>
    public string TextLanguageName(string code) => this[code == "es" ? "lang.spanish" : "lang.english"];
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LocaleManifest))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocJsonContext : JsonSerializerContext;

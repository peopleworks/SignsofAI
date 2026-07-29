using System.Text.Json;
using System.Text.RegularExpressions;
using SignsOfAI.Core.Model;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Guards the community-contributed UI translations in <c>src/SignsOfAI.Web/wwwroot/i18n/</c>.
///
/// These files are data, not code, so nothing stops a well-meaning pull request from mistyping a
/// key, dropping a <c>{0}</c> placeholder or duplicating an entry — mistakes that surface only as
/// odd text on a live page. These tests make a translation PR reviewable: the failure message says
/// exactly which key in which file is wrong.
///
/// A translation is allowed to be *incomplete* — missing keys fall back to English at runtime, so a
/// partial contribution is still worth merging. What is not allowed is being *wrong*.
/// </summary>
public class LocaleFileTests
{
    private static readonly string I18nDir = FindI18nDirectory();
    private static readonly string WebSrcDir =
        Path.GetFullPath(Path.Combine(I18nDir, "..", ".."));

    // Keys the C# / Razor code builds at run time from an enum or a score band, so a plain text
    // search can't see them.
    private static readonly string[] ComputedKeys =
    [
        .. Enum.GetValues<SignCategory>().Select(c => "cat." + c.ToString().ToLowerInvariant()),
        .. Enum.GetValues<Severity>().Select(s => "sev." + s.ToString().ToLowerInvariant()),
        "verdict.strong", "verdict.moderate", "verdict.light", "verdict.minimal",
        "lang.english", "lang.spanish",
    ];

    [Fact]
    public void Manifest_lists_a_fallback_and_a_file_for_every_locale()
    {
        var manifest = ReadManifest();

        Assert.NotEmpty(manifest.Locales);
        Assert.Contains(manifest.Fallback, manifest.Locales.Select(l => l.Code));

        foreach (var locale in manifest.Locales)
        {
            Assert.False(string.IsNullOrWhiteSpace(locale.Code), "A locale entry is missing its \"code\".");
            Assert.False(string.IsNullOrWhiteSpace(locale.Endonym),
                $"Locale \"{locale.Code}\" needs an \"endonym\" — its name in its own language.");
            Assert.True(File.Exists(Path.Combine(I18nDir, locale.Code + ".json")),
                $"locales.json offers \"{locale.Code}\" but {locale.Code}.json does not exist.");
        }

        var duplicates = manifest.Locales
            .GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_locale_file_is_valid_json_with_no_duplicate_or_blank_entries()
    {
        foreach (var file in LocaleFiles())
        {
            var name = Path.GetFileName(file);
            using var doc = JsonDocument.Parse(File.ReadAllText(file));

            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);

            var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            var dupes = names.GroupBy(n => n, StringComparer.Ordinal)
                             .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(dupes.Count == 0,
                $"{name} repeats these keys (the later one silently wins): {string.Join(", ", dupes)}");

            var blanks = doc.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind != JsonValueKind.String
                            || string.IsNullOrWhiteSpace(p.Value.GetString()))
                .Select(p => p.Name).ToList();
            Assert.True(blanks.Count == 0,
                $"{name} has blank or non-string values for: {string.Join(", ", blanks)}. " +
                "Delete the entry instead — it will fall back to English.");
        }
    }

    [Fact]
    public void Fallback_locale_defines_every_key_the_app_asks_for()
    {
        var manifest = ReadManifest();
        var fallback = ReadLocale(manifest.Fallback);

        var used = KeysUsedInSource().Concat(ComputedKeys).Distinct(StringComparer.Ordinal).OrderBy(k => k);
        var missing = used.Where(k => !fallback.ContainsKey(k)).ToList();

        Assert.True(missing.Count == 0,
            $"{manifest.Fallback}.json is missing keys the UI requests (they would render as the raw " +
            $"key text): {string.Join(", ", missing)}");
    }

    [Fact]
    public void Translations_use_only_known_keys()
    {
        var manifest = ReadManifest();
        var fallback = ReadLocale(manifest.Fallback);

        foreach (var locale in manifest.Locales.Where(l => l.Code != manifest.Fallback))
        {
            var strings = ReadLocale(locale.Code);
            var unknown = strings.Keys.Where(k => !fallback.ContainsKey(k)).OrderBy(k => k).ToList();

            Assert.True(unknown.Count == 0,
                $"{locale.Code}.json defines keys that do not exist in {manifest.Fallback}.json, so they " +
                $"are never displayed — likely a typo or a stale entry: {string.Join(", ", unknown)}");
        }
    }

    [Fact]
    public void Translations_keep_the_same_placeholders_as_the_fallback()
    {
        var manifest = ReadManifest();
        var fallback = ReadLocale(manifest.Fallback);
        var problems = new List<string>();

        foreach (var locale in manifest.Locales.Where(l => l.Code != manifest.Fallback))
        {
            foreach (var (key, translated) in ReadLocale(locale.Code))
            {
                if (!fallback.TryGetValue(key, out var original)) continue; // covered by the previous test

                var expected = Placeholders(original);
                var actual = Placeholders(translated);
                if (!expected.SetEquals(actual))
                {
                    problems.Add(
                        $"{locale.Code}.json → \"{key}\": expected {Show(expected)} but found {Show(actual)}");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "A translation dropped or invented a placeholder, so the value it stands for would go " +
            $"missing on screen:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    /// <summary>Reported, never failed: a partial translation is still worth shipping.</summary>
    [Fact]
    public void Report_translation_completeness()
    {
        var manifest = ReadManifest();
        var fallback = ReadLocale(manifest.Fallback);

        foreach (var locale in manifest.Locales)
        {
            var strings = ReadLocale(locale.Code);
            var done = fallback.Keys.Count(k => strings.ContainsKey(k));
            var pct = fallback.Count == 0 ? 100 : done * 100 / fallback.Count;
            Assert.InRange(pct, 0, 100);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static readonly Regex PlaceholderPattern = new(@"\{(\d+)\}", RegexOptions.Compiled);

    private static HashSet<string> Placeholders(string value) =>
        PlaceholderPattern.Matches(value).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

    private static string Show(HashSet<string> set) =>
        set.Count == 0 ? "none" : string.Join(" ", set.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>Every key the Razor/C# UI looks up as a literal, including the two plural variants.</summary>
    private static IEnumerable<string> KeysUsedInSource()
    {
        // L["key"] · L.M("key" · L.F("key" · L.P(n, "key")
        var direct = new Regex(
            @"L\s*\[\s*""(?<k>[^""]+)""\s*\]|L\.M\(\s*""(?<k>[^""]+)""|L\.F\(\s*""(?<k>[^""]+)""",
            RegexOptions.Compiled);
        var plural = new Regex(@"L\.P\([^,]+,\s*""(?<k>[^""]+)""", RegexOptions.Compiled);

        var files = Directory.EnumerateFiles(WebSrcDir, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(WebSrcDir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            foreach (Match m in direct.Matches(text))
            {
                var key = m.Groups["k"].Value;
                // The doc comments in Loc/LocalizedComponent show L["key"] as an example.
                if (key != "key") yield return key;
            }

            foreach (Match m in plural.Matches(text))
            {
                yield return m.Groups["k"].Value + ".one";
                yield return m.Groups["k"].Value + ".other";
            }
        }
    }

    private static IEnumerable<string> LocaleFiles() =>
        Directory.EnumerateFiles(I18nDir, "*.json")
            .Where(f => !string.Equals(Path.GetFileName(f), "locales.json", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string> ReadLocale(string code)
    {
        var path = Path.Combine(I18nDir, code + ".json");
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
        Assert.NotNull(parsed);
        return parsed;
    }

    private sealed record ManifestEntry(string Code, string Name, string Endonym, string Credit);
    private sealed record Manifest(string Fallback, ManifestEntry[] Locales);

    private static Manifest ReadManifest()
    {
        var json = File.ReadAllText(Path.Combine(I18nDir, "locales.json"));
        var parsed = JsonSerializer.Deserialize<Manifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(parsed);
        return parsed;
    }

    /// <summary>Walks up from the test binaries to the repository root, then down to the locale folder.</summary>
    private static string FindI18nDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SignsOfAI.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "src", "SignsOfAI.Web", "wwwroot", "i18n");
        Assert.True(Directory.Exists(path), $"Expected the locale folder at {path}");
        return path;
    }
}

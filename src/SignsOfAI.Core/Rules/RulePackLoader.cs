using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace SignsOfAI.Core.Rules;

/// <summary>Loads and caches language rule-packs from embedded JSON resources.</summary>
public static class RulePackLoader
{
    private static readonly ConcurrentDictionary<string, RulePack> Cache = new();

    /// <summary>Loads the rule-pack for a language code, falling back to English.</summary>
    public static RulePack Load(string language) => Resolve(language).Pack;

    /// <summary>
    /// The pack, and the language it was actually built for.
    ///
    /// These differ whenever a language has no pack yet, and the difference has to travel: a text
    /// analysed with the English catalog is not a French analysis, and a result that claimed to be
    /// one would be saying nothing fired in French when nothing French was ever looked for. Rule
    /// packs are files anyone can add, so this is the ordinary case for a new language rather than
    /// an error.
    /// </summary>
    public static (RulePack Pack, string Language) Resolve(string? language)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant();
        return (Cache.GetOrAdd(lang, LoadFromResource), Available(lang) ? lang : "en");
    }

    /// <summary>Whether a built-in pack exists for this language. Adding one is adding a file.</summary>
    public static bool Available(string? language) =>
        !string.IsNullOrWhiteSpace(language)
        && typeof(RulePackLoader).Assembly.GetManifestResourceInfo(ResourceName(language)) is not null;

    /// <summary>
    /// Every language with a built-in pack, so hosts can offer what exists rather than a hardcoded
    /// pair that a contributor cannot extend.
    /// </summary>
    public static IReadOnlyList<string> Languages { get; } =
        [.. typeof(RulePackLoader).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..^".json".Length])
            .Where(n => n.Length is > 0 and <= 12 && n.All(char.IsAsciiLetterLower))
            .Order(StringComparer.Ordinal)];

    private const string Prefix = "SignsOfAI.Core.Rules.Packs.rules.";

    private static string ResourceName(string language) =>
        $"SignsOfAI.Core.Rules.Packs.rules.{language.ToLowerInvariant()}.json";

    private static RulePack LoadFromResource(string language)
    {
        var asm = typeof(RulePackLoader).Assembly;
        var resourceName = ResourceName(language);

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? (language != "en"
                ? asm.GetManifestResourceStream(ResourceName("en"))
                : null)
            ?? throw new InvalidOperationException(
                $"Rule-pack resource '{resourceName}' not found. Available: " +
                string.Join(", ", asm.GetManifestResourceNames()));

        var pack = JsonSerializer.Deserialize(stream, RulePackJsonContext.Default.RulePack)
            ?? throw new InvalidOperationException($"Rule-pack '{resourceName}' deserialized to null.");
        return pack;
    }
}

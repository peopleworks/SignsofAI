using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace SignsOfAI.Core.Rules;

/// <summary>Loads and caches language rule-packs from embedded JSON resources.</summary>
public static class RulePackLoader
{
    private static readonly ConcurrentDictionary<string, RulePack> Cache = new();

    /// <summary>Loads the rule-pack for a language code ("en"/"es"), falling back to English.</summary>
    public static RulePack Load(string language)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant();
        return Cache.GetOrAdd(lang, LoadFromResource);
    }

    private static RulePack LoadFromResource(string language)
    {
        var asm = typeof(RulePackLoader).Assembly;
        var resourceName = $"SignsOfAI.Core.Rules.Packs.rules.{language}.json";

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? (language != "en"
                ? asm.GetManifestResourceStream("SignsOfAI.Core.Rules.Packs.rules.en.json")
                : null)
            ?? throw new InvalidOperationException(
                $"Rule-pack resource '{resourceName}' not found. Available: " +
                string.Join(", ", asm.GetManifestResourceNames()));

        var pack = JsonSerializer.Deserialize(stream, RulePackJsonContext.Default.RulePack)
            ?? throw new InvalidOperationException($"Rule-pack '{resourceName}' deserialized to null.");
        return pack;
    }
}

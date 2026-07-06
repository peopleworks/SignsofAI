using System.Text.Json;
using System.Text.Json.Serialization;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Rules;

/// <summary>A vocabulary rule: one concept with all its surface forms.</summary>
public sealed class LexicalRule
{
    public required string Id { get; init; }

    /// <summary>Surface forms to match, case-insensitively (e.g. ["delve","delves","delving"]).</summary>
    public required string[] Terms { get; init; }

    public double Weight { get; init; } = 3.0;

    public Severity Severity { get; init; } = Severity.Medium;

    /// <summary>Comma-separated human-friendly alternatives.</summary>
    public required string Suggestion { get; init; }

    /// <summary>Optional supporting evidence shown to the user.</summary>
    public string? Evidence { get; init; }
}

/// <summary>A regex rule for rhetorical/syntactic patterns spanning multiple words.</summary>
public sealed class PatternRule
{
    public required string Id { get; init; }

    public required SignCategory Category { get; init; }

    /// <summary>.NET regex, matched case-insensitively.</summary>
    public required string Regex { get; init; }

    public double Weight { get; init; } = 4.0;

    public Severity Severity { get; init; } = Severity.Medium;

    public required string Message { get; init; }

    public required string Suggestion { get; init; }

    public string? Evidence { get; init; }
}

/// <summary>A full rule-pack (a "catalog") — built-in or supplied by the user.</summary>
public sealed class RulePack
{
    /// <summary>"en", "es", or "*"/"all"/empty for a language-agnostic custom catalog.</summary>
    public required string Language { get; init; }

    public LexicalRule[] Lexical { get; init; } = [];

    public PatternRule[] Patterns { get; init; } = [];

    /// <summary>Parse a rule-pack from JSON (the same schema as the built-in packs).</summary>
    public static RulePack FromJson(string json) =>
        JsonSerializer.Deserialize(json, RulePackJsonContext.Default.RulePack)
        ?? throw new InvalidOperationException("Rule-pack JSON deserialized to null.");

    public string ToJson() => JsonSerializer.Serialize(this, RulePackJsonContext.Default.RulePack);

    /// <summary>
    /// Combine several catalogs into one. Rules are keyed by <c>Id</c>, so a later pack overrides an
    /// earlier one with the same id — this lets a custom catalog tweak or replace a built-in rule.
    /// </summary>
    public static RulePack Merge(string language, IEnumerable<RulePack> packs)
    {
        var lexical = new Dictionary<string, LexicalRule>(StringComparer.Ordinal);
        var patterns = new Dictionary<string, PatternRule>(StringComparer.Ordinal);
        foreach (var pack in packs)
        {
            // A custom pack parsed from JSON may omit a section, leaving the array null under source-gen.
            foreach (var rule in pack.Lexical ?? []) lexical[rule.Id] = rule;
            foreach (var rule in pack.Patterns ?? []) patterns[rule.Id] = rule;
        }
        return new RulePack
        {
            Language = language,
            Lexical = [.. lexical.Values],
            Patterns = [.. patterns.Values],
        };
    }

    /// <summary>Whether this catalog applies to a given detected language.</summary>
    public bool AppliesTo(string language) =>
        string.IsNullOrWhiteSpace(Language) || Language is "*" or "all"
        || Language.Equals(language, StringComparison.OrdinalIgnoreCase);
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(RulePack))]
public partial class RulePackJsonContext : JsonSerializerContext;

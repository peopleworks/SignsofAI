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

/// <summary>A full language rule-pack, deserialized from an embedded JSON resource.</summary>
public sealed class RulePack
{
    public required string Language { get; init; }

    public LexicalRule[] Lexical { get; init; } = [];

    public PatternRule[] Patterns { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(RulePack))]
public partial class RulePackJsonContext : JsonSerializerContext;

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

    /// <summary>
    /// Machine-applicable replacements, best first, for the live rewriter. <see cref="Suggestion"/> is
    /// prose written for a person ("mix, blend, range — or just name the thing"); this is the subset a
    /// program can actually substitute. Optional: when omitted, the rewriter falls back to reading the
    /// leading comma-separated terms out of <see cref="Suggestion"/>, so third-party catalogs written
    /// before this field existed still work.
    /// </summary>
    public string[]? Replacements { get; init; }

    /// <summary>
    /// True when the fix is to delete the word rather than swap it — the empty intensifiers ("just",
    /// "simply", "realmente"). Deliberately explicit rather than inferred from <see cref="Suggestion"/>,
    /// whose wording is language-specific ("usually deletable" / "suele sobrar") and would silently
    /// fail for any language the packs don't ship.
    /// </summary>
    public bool Delete { get; init; }

    /// <summary>Optional supporting evidence shown to the user.</summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// What the live rewriter can substitute for a match, best first. Empty when this rule has no
    /// mechanical fix (the writer has to make a judgement call), which the rewriter treats as
    /// "highlight it, don't touch it".
    /// </summary>
    public IReadOnlyList<string> RewriteOptions() =>
        Replacements is { Length: > 0 } explicitOnes
            ? explicitOnes
            : SuggestionParser.LeadingTerms(Suggestion);
}

/// <summary>
/// Salvages machine-applicable replacements from a prose <c>suggestion</c>, for catalogs that predate
/// <see cref="LexicalRule.Replacements"/> — including ones contributed by users.
///
/// Deliberately conservative and language-neutral: it takes the leading comma-separated terms and
/// stops at the first aside, and it never infers a *deletion*. Guessing wrong here would silently
/// change someone's prose in a way they didn't ask for, so anything ambiguous yields nothing and the
/// rewriter leaves the word alone.
/// </summary>
public static class SuggestionParser
{
    // An aside begins at a dash ("mix, blend — or just name the thing") or at an "or"-clause
    // ("complex, or specify the actual facets"). Everything from there on is advice, not a term.
    private static readonly string[] AsideMarkers =
        ["—", " – ", " -- ", ", or ", ", o ", " or just ", " o just "];

    private const int MaxWordsPerTerm = 4; // "a lot of", "lo más avanzado" — beyond this it's prose

    public static IReadOnlyList<string> LeadingTerms(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion)) return [];

        var head = suggestion;
        foreach (var marker in AsideMarkers)
        {
            var at = head.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at >= 0) head = head[..at];
        }

        var terms = head
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 0
                           && term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= MaxWordsPerTerm
                           && !term.Contains('(') && !term.Contains(':'))
            .ToList();

        // Only a comma-separated list of alternatives is unmistakable. A single term is not: nothing
        // short of knowing the language separates the replacement "use" from the description
        // "muletilla" (Spanish for "filler word"), and substituting the latter into someone's sentence
        // would be far worse than leaving the word alone. So a lone term is refused here, and any rule
        // wanting a single replacement states it in `replacements` — which every built-in rule does.
        return terms.Count >= 2 ? terms : [];
    }
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

    /// <summary>
    /// Wording for the analyzers that compute their findings instead of matching a rule — the
    /// overused-word message, the rhythm one, the em-dash one. Optional: anything missing falls back
    /// to <see cref="PackMessages.Defaults"/>, so a pack written before this existed still works.
    /// </summary>
    public Dictionary<string, string>? Messages { get; init; }

    /// <summary>
    /// A template from this pack, filled in. Falls back to the built-in English when the pack does
    /// not carry the key.
    ///
    /// A bad placeholder in a community-contributed template returns the raw template rather than
    /// throwing — the same choice the interface translations make. One mistyped brace should cost a
    /// clumsy sentence, not a blank page.
    /// </summary>
    public string Text(string key, params object?[] args)
    {
        var template = Messages is not null && Messages.TryGetValue(key, out var custom)
                       && !string.IsNullOrWhiteSpace(custom)
            ? custom
            : PackMessages.Defaults.TryGetValue(key, out var fallback) ? fallback : key;

        if (args.Length == 0) return template;
        try { return string.Format(template, args); }
        catch (FormatException) { return template; }
    }

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
        var messages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pack in packs)
        {
            // A custom pack parsed from JSON may omit a section, leaving the array null under source-gen.
            foreach (var rule in pack.Lexical ?? []) lexical[rule.Id] = rule;
            foreach (var rule in pack.Patterns ?? []) patterns[rule.Id] = rule;
            // Merged key by key, so a custom catalog can reword one message without restating them all.
            foreach (var (key, text) in pack.Messages ?? []) messages[key] = text;
        }
        return new RulePack
        {
            Language = language,
            Lexical = [.. lexical.Values],
            Patterns = [.. patterns.Values],
            Messages = messages.Count > 0 ? messages : null,
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

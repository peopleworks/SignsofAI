using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Rules;

/// <summary>A flattened, human-browsable description of one rule (lexical or pattern).</summary>
public sealed record SignInfo
{
    public required string Id { get; init; }
    public required string Language { get; init; }
    public required SignCategory Category { get; init; }
    public required Severity Severity { get; init; }

    /// <summary>Short display title (a representative term or the pattern's plain name).</summary>
    public required string Title { get; init; }

    /// <summary>Example surface forms or a sample of what it matches.</summary>
    public required string[] Examples { get; init; }

    public required string Message { get; init; }
    public required string Suggestion { get; init; }
    public string? Evidence { get; init; }

    /// <summary>All searchable text, concatenated once for indexing.</summary>
    public string SearchText =>
        $"{Title} {string.Join(' ', Examples)} {Message} {Suggestion} {Evidence} {Category}";
}

/// <summary>Builds a browsable catalog of every rule across the language packs.</summary>
public static class RuleCatalog
{
    public static IReadOnlyList<SignInfo> ForLanguage(string language)
    {
        var pack = RulePackLoader.Load(language);
        var items = new List<SignInfo>();

        foreach (var rule in pack.Lexical)
        {
            items.Add(new SignInfo
            {
                Id = rule.Id,
                Language = pack.Language,
                Category = SignCategory.Lexical,
                Severity = rule.Severity,
                Title = rule.Terms.FirstOrDefault() ?? rule.Id,
                Examples = rule.Terms,
                Message = "Overused AI vocabulary.",
                Suggestion = rule.Suggestion,
                Evidence = rule.Evidence,
            });
        }

        foreach (var rule in pack.Patterns)
        {
            items.Add(new SignInfo
            {
                Id = rule.Id,
                Language = pack.Language,
                Category = rule.Category,
                Severity = rule.Severity,
                Title = TitleFromMessage(rule.Message),
                Examples = [],
                Message = rule.Message,
                Suggestion = rule.Suggestion,
                Evidence = rule.Evidence,
            });
        }

        return items;
    }

    /// <summary>Both language packs, English first.</summary>
    public static IReadOnlyList<SignInfo> All() =>
        [.. ForLanguage("en"), .. ForLanguage("es")];

    // Pattern messages read like "Negative parallelism (“…”)." — take the lead phrase as a title.
    private static string TitleFromMessage(string message)
    {
        int paren = message.IndexOf('(');
        var head = (paren > 0 ? message[..paren] : message).Trim();
        return head.TrimEnd('.', ':', ' ');
    }
}

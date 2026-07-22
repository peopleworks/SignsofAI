using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core.Rules;

namespace SignsOfAI.Mcp.Tools;

/// <summary>Browse/search the catalog of AI-writing signs the analyzer knows about. Offline reference.</summary>
[McpServerToolType]
public static class CatalogTools
{
    // The catalog is built once from the embedded rule-packs.
    private static readonly IReadOnlyList<SignInfo> All = RuleCatalog.All();

    [McpServerTool(Name = "search_catalog", ReadOnly = true),
     Description("""
        Searches the catalog of AI-writing "signs" the analyzer looks for (English & Spanish) — each with why it
        reads as AI and how to fix it. Useful as a reference / study aid, or to explain a finding in depth. Filter
        by keyword, language ("en"/"es"), and/or category (Lexical, Rhetorical, Syntactic, Statistical). Offline.
        """)]
    public static CatalogResult SearchCatalog(
        [Description("Keyword filter (matches title, examples, message, suggestion). Empty = all.")] string query = "",
        [Description("Language filter: \"en\", \"es\", or empty for both.")] string language = "",
        [Description("Category filter: Lexical, Rhetorical, Syntactic, Statistical, or empty.")] string category = "")
    {
        IEnumerable<SignInfo> items = All;

        if (!string.IsNullOrWhiteSpace(language))
            items = items.Where(s => s.Language.Equals(language.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(category))
            items = items.Where(s => s.Category.ToString().Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            items = items.Where(s => s.SearchText.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var entries = items
            .Take(60)
            .Select(s => new CatalogEntry(
                s.Id, s.Language, s.Category.ToString(), s.Severity.ToString(), s.Title, s.Examples, s.Message, s.Suggestion, s.Evidence))
            .ToList();

        return new CatalogResult(entries.Count, entries);
    }
}

public sealed record CatalogResult(int Count, IReadOnlyList<CatalogEntry> Entries);

public sealed record CatalogEntry(
    string Id,
    string Language,
    string Category,
    string Severity,
    string Title,
    string[] Examples,
    string Message,
    string Suggestion,
    string? Evidence);

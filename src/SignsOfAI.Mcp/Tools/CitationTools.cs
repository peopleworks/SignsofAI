using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core.Citations;

namespace SignsOfAI.Mcp.Tools;

/// <summary>
/// The citation cross-check, on its own tool.
///
/// An agent walking someone through a disputed essay needs this question separated from the score.
/// "Does this read like AI" comes back as a probability that no integrity committee can act on;
/// "the text cites Martínez (2019) and no Martínez appears in its own bibliography" comes back as
/// something the writer settles in one sentence. Keeping them apart is what stops the second from
/// being reported with the hedging that belongs to the first.
/// </summary>
[McpServerToolType]
public static class CitationTools
{
    [McpServerTool(Name = "check_citations", ReadOnly = true),
     Description("""
        Compares a document against its own reference list and reports where the two disagree: a source cited
        in the text that appears nowhere in the bibliography, a number cited beyond the end of a numbered list,
        one DOI on two different works, a malformed DOI, a publication year that has not happened yet, a
        duplicated entry. Works for English and Spanish, numbered (IEEE/Vancouver) and author-year (APA/MLA)
        styles, and returns the line of every problem.
        Runs FULLY OFFLINE and looks nothing up: it cannot tell you whether a well-formed reference is a real
        paper, only whether the document contradicts itself. That is often enough, because an invented
        bibliography tends to fail against itself first. Nothing is sent anywhere.
        A missing reference is usually a slip rather than dishonesty, and it is always the writer's to explain
        — the correct response to a finding is to ask them for the source.
        """)]
    public static CitationCheck CheckCitations(
        [Description("The document, including its reference list.")] string text,
        [Description("Language for the wording of the messages: \"en\" or \"es\". Default \"en\".")] string language = "en",
        [Description("What counts as the future, for the impossible-year check. Omit to use the current year.")] int? currentYear = null)
    {
        var pack = Core.Rules.RulePackLoader.Load(string.IsNullOrWhiteSpace(language) ? "en" : language);
        var report = CitationChecker.Check(text ?? string.Empty, pack, currentYear);

        return new CitationCheck(
            report.Style.ToString(),
            report.HasReferenceList,
            report.References.Count,
            report.Citations.Count,
            report.ContradictionCount,
            report.Any ? report.Summary : "This document points at no sources at all.",
            report.Advice,
            report.Issues
                .Select(i => new CitationProblem(i.Kind.ToString(), i.Line, i.Subject, i.Message, i.IsContradiction))
                .ToList(),
            report.References
                .Select(r => new ListedReference(r.Number, r.Line, r.LeadSurname, r.Year, r.Doi, r.Raw))
                .ToList());
    }
}

/// <param name="Style">"Numbered", "AuthorYear", "Mixed" or "None".</param>
/// <param name="HasReferenceList">
/// False when no reference section could be identified, in which case the cross-checks were not run
/// at all rather than guessed at.
/// </param>
/// <param name="ContradictionCount">
/// Problems that are a plain contradiction in the document. Excludes the untidy ones — an entry
/// nobody cited is normal, because people list further reading.
/// </param>
public sealed record CitationCheck(
    string Style,
    bool HasReferenceList,
    int ReferenceCount,
    int CitationCount,
    int ContradictionCount,
    string Summary,
    string Advice,
    IReadOnlyList<CitationProblem> Problems,
    IReadOnlyList<ListedReference> References);

public sealed record CitationProblem(
    string Kind, int Line, string Subject, string Message, bool IsContradiction);

public sealed record ListedReference(
    int? Number, int Line, string? LeadSurname, int? Year, string? Doi, string Raw);

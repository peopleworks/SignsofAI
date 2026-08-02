using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core.Artifacts;

namespace SignsOfAI.Mcp.Tools;

/// <summary>
/// The character-artifact scan, exposed on its own.
///
/// It is a separate tool rather than a corner of the analysis because it answers a different kind of
/// question. "Does this read like AI" is a judgement and comes back as a score; "is there a Cyrillic
/// letter at line 14, column 3" is a fact and comes back as coordinates. An agent helping someone
/// work through a disputed document needs to be able to ask the second question without dragging in
/// the first, and to hand back an answer the person can verify in any text editor.
/// </summary>
[McpServerToolType]
public static class ArtifactTools
{
    [McpServerTool(Name = "inspect_characters", ReadOnly = true),
     Description("""
        Reports characters present in a text that typing does not produce: invisible/zero-width characters,
        letters borrowed from another alphabet to impersonate Latin ones (a Cyrillic "а" for an "a"), text
        direction controls, and hidden tag characters. Tools that rewrite text to defeat AI detectors insert
        these deliberately. Returns the exact codepoint, line and column of every occurrence, plus whether they
        are clustered (which ordinary copy-paste from a web page or a PDF produces) or spread through the whole
        document (which is what a rewriting tool leaves behind). Language-independent and fully offline.
        This is a checkable fact about a file, NOT proof of dishonesty and NOT a claim about who wrote the text:
        legitimate documents pick these up from PDFs, web pages and multilingual writing. The correct response
        to a finding is to ask the writer how the document was produced.
        """)]
    public static CharacterReport InspectCharacters(
        [Description("The text to inspect, exactly as it arrived — not a cleaned copy.")] string text,
        [Description("Language for the wording of the messages: \"en\" or \"es\". Default \"en\".")] string language = "en")
    {
        var pack = Core.Rules.RulePackLoader.Load(string.IsNullOrWhiteSpace(language) ? "en" : language);
        var report = ArtifactScanner.Scan(text ?? string.Empty, pack);

        return new CharacterReport(
            report.Pattern.ToString(),
            report.Count,
            report.StrongCount,
            report.SectionsAffected,
            report.SectionCount,
            report.Any ? report.Summary : "Nothing unusual: every character in this text is one that writing it produces.",
            report.Advice,
            report.Groups
                .Select(g => new CharacterGroup(g.Kind.ToString(), g.CodePoint, g.CharacterName, g.LooksLike, g.Count, g.IsStrong))
                .ToList(),
            report.Occurrences
                .Select(o => new CharacterAt(o.Kind.ToString(), o.CodePoint, o.CharacterName,
                    o.LooksLike, o.Word, o.Line, o.Column, o.Span.Start, o.IsStrong))
                .ToList());
    }
}

/// <param name="Pattern">"None", "Incidental" (clustered) or "Systematic" (spread through the document).</param>
/// <param name="StrongCount">Occurrences of the kinds that ordinary copy-paste does not produce.</param>
public sealed record CharacterReport(
    string Pattern,
    int Count,
    int StrongCount,
    int SectionsAffected,
    int SectionCount,
    string Summary,
    string Advice,
    IReadOnlyList<CharacterGroup> Groups,
    IReadOnlyList<CharacterAt> Occurrences);

public sealed record CharacterGroup(
    string Kind, string CodePoint, string CharacterName, string? LooksLike, int Count, bool IsStrong);

/// <param name="Line">1-based, so it matches what a text editor shows.</param>
public sealed record CharacterAt(
    string Kind, string CodePoint, string CharacterName, string? LooksLike, string? Word,
    int Line, int Column, int Offset, bool IsStrong);

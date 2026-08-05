using System.ComponentModel;
using ModelContextProtocol.Server;
using SignsOfAI.Core;
using SignsOfAI.Core.Reporting;

namespace SignsOfAI.Mcp.Tools;

/// <summary>
/// The analysis as a document a person can keep, rather than a payload for a model.
///
/// The other tools answer questions; this one produces the artefact. An agent asked to look over a
/// stack of coursework has, until now, been able to describe what it found and nothing more — and a
/// description in a chat window is exactly the thing a teacher cannot forward to a student or take to
/// an integrity committee. Handing back the finished document, with its own error rate printed on it,
/// is the difference between an agent that reports and one that is useful on Monday.
///
/// Markdown rather than the HTML variant, deliberately. Markdown survives being pasted into an email,
/// a comment box or an issue, and it is legible when the agent quotes part of it back. A self-contained
/// HTML page is the better artefact for printing, and the CLI and both apps produce it; pushing tens of
/// kilobytes of markup through a tool result to a model that will not read it would be waste.
/// </summary>
[McpServerToolType]
public static class ReportTools
{
    [McpServerTool(Name = "write_report", ReadOnly = true),
     Description("""
        Produces the full analysis as a Markdown document a person can keep, forward to a writer, or take to
        an academic-integrity committee — the finished artefact rather than a summary to paraphrase.
        It contains the score, the signals that counted and the ones found at a rate people write at, the
        characters found in the file with their line and column, and the places where the document's citations
        disagree with its own bibliography. Checkable facts are named at the top and kept apart from the score,
        which is an opinion about prose.
        Every report prints how often this build is wrong, measured for the language actually analysed against
        texts published before generative models existed, and names the rules known to fire on human writing so
        the reader can weigh evidence that leans on one. Below the threshold that measurement supports, no
        verdict is given at all.
        Runs FULLY OFFLINE. The result contains material from the document, so treat it as you would the
        coursework itself: hand it to the person who asked, do not post it anywhere.
        Prefer this over paraphrasing the other tools' output when the user wants something to send, save,
        print or attach.
        """)]
    public static string WriteReport(
        [Description("The document to analyse and describe.")] string text,
        [Description("\"en\", \"es\", or \"auto\" to detect. Defaults to auto.")] string? language = null,
        [Description("Name of the file or assignment, printed on the report. Optional.")] string? documentName = null,
        [Description("Title for the document. Defaults to a title in the report language.")] string? title = null,
        [Description("Reader-facing report language: \"en\" or \"es\". Independent from the analysed text. Defaults to English.")] string? interfaceLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "No text was supplied, so there is nothing to report on.";

        var result = new AiWritingAnalyzer().Analyze(text, language);

        var options = new ReportOptions
        {
            DocumentName = documentName ?? "",
            Title = string.IsNullOrWhiteSpace(title) ? ReportOptions.Default.Title : title,
            InterfaceLanguage = string.IsNullOrWhiteSpace(interfaceLanguage) ? "en" : interfaceLanguage,
        };

        return EvidenceReport.ToMarkdown(result, options);
    }
}

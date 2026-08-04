using System.Globalization;
using System.Text;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Reporting;

/// <summary>
/// The analysis as a document somebody can keep, forward, print or attach.
///
/// Until this existed the evidence died when the tab closed. That is a hole in the argument rather
/// than a missing feature: this project's entire claim is that a percentage is not evidence and that
/// it will show you the actual tells, the hidden characters at their line and column, the citations a
/// document makes that its own bibliography contradicts. A teacher who cannot forward any of that to
/// a student, or take it to an integrity committee, has been shown something and handed nothing.
///
/// Three rules govern what comes out, and none of them is cosmetic.
///
/// <para><b>It prints its own error rate.</b> Every report carries the threshold this build
/// recommends, how it was measured, and the upper bound of the interval — not the observed rate. A
/// document that accuses somebody without saying how often the method is wrong is the artefact this
/// project was built to argue against, and producing one with a nicer layout would be worse than
/// producing none.</para>
///
/// <para><b>It separates facts from judgements.</b> The score is an opinion about prose. A character
/// at line 37 column 64, and a citation missing from the reference list beside it, are neither
/// opinions nor percentages: they are either there or they are not. They are printed apart, and they
/// are the part a committee can actually act on.</para>
///
/// <para><b>It contains the text, so it says so.</b> The report is generated on the reader's own
/// device and saved by them. That has to be on the page, because the share card — the other thing
/// this tool exports — is built for posting in public, and confusing the two would publish a
/// student's work.</para>
/// </summary>
public static class EvidenceReport
{
    /// <summary>
    /// The report as Markdown: for pasting into an email, an LMS comment box or an issue. Deliberately
    /// the primary form — it survives being quoted, and a teacher can delete the half they do not need
    /// before sending it, which nobody can do with a PDF.
    /// </summary>
    public static string ToMarkdown(AnalysisResult result, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var sb = new StringBuilder();

        sb.Append("# ").Append(o.Title).AppendLine();
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(o.DocumentName))
            sb.Append("**Document:** ").AppendLine(o.DocumentName);
        sb.Append("**Generated:** ").Append(o.GeneratedOn).Append(" · **Engine:** SignsOfAI ")
          .AppendLine(o.EngineVersion);
        sb.AppendLine();

        // ── The reading, and immediately the caveat that makes it usable ──────────────────────────
        sb.AppendLine("## What the analysis says");
        sb.AppendLine();
        sb.Append("**").Append(Num(result.OverallScore)).Append("/100 — ").Append(result.Verdict)
          .AppendLine("**");
        sb.AppendLine();
        sb.Append("- ").Append(result.Signals.Count).Append(" signal")
          .Append(result.Signals.Count == 1 ? "" : "s").Append(" counted");
        if (result.Observations.Count > 0)
            sb.Append(", plus ").Append(result.Observations.Count)
              .Append(" found at a rate people write at, which count for nothing");
        sb.AppendLine();
        sb.Append("- Analysed as ").Append(result.Language == "es" ? "Spanish" : "English")
          .Append(" · ").Append(result.Statistics.WordCount).Append(" words · ")
          .Append(result.Statistics.SentenceCount).Append(" sentences · sentence-length variability ")
          .AppendLine(Num(result.Statistics.Burstiness, 2));
        sb.AppendLine();
        sb.AppendLine(Caveat());
        sb.AppendLine();

        // ── Checkable facts first, because they are the part that settles anything ────────────────
        if (result.Artifacts.Any || result.Citations.Any)
        {
            sb.AppendLine("## Checkable facts");
            sb.AppendLine();
            sb.AppendLine("These are not judgements about the writing and they did not move the score. " +
                          "Each is either present in the file or it is not.");
            sb.AppendLine();

            if (result.Artifacts.Any)
            {
                sb.AppendLine("### Characters that writing does not produce");
                sb.AppendLine();
                sb.AppendLine(result.Artifacts.Summary);
                sb.AppendLine();
                sb.AppendLine("| Character | Codepoint | Line | Column |");
                sb.AppendLine("|---|---|---:|---:|");
                foreach (var occurrence in result.Artifacts.Occurrences.Take(o.MaxRows))
                    sb.Append("| ").Append(Describe(occurrence.Kind)).Append(" | `")
                      .Append(occurrence.CodePoint).Append("` | ").Append(occurrence.Line)
                      .Append(" | ").Append(occurrence.Column).AppendLine(" |");
                if (result.Artifacts.Occurrences.Count > o.MaxRows)
                    sb.Append("\n… and ").Append(result.Artifacts.Occurrences.Count - o.MaxRows)
                      .AppendLine(" more.");
                sb.AppendLine();
            }

            if (result.Citations.Any)
            {
                sb.AppendLine("### What the document says about its own sources");
                sb.AppendLine();
                sb.AppendLine(result.Citations.Summary);
                sb.AppendLine();
                foreach (var issue in result.Citations.Issues.Take(o.MaxRows))
                    sb.Append("- ").AppendLine(issue.Message);
                sb.AppendLine();
                sb.AppendLine("> None of this needed the internet: the document contradicts itself. " +
                              "It is a question to ask, not a conclusion — the answer is usually one sentence.");
                sb.AppendLine();
            }
        }

        // ── The judgement, clearly labelled as one ───────────────────────────────────────────────
        sb.AppendLine("## Signals counted");
        sb.AppendLine();
        if (result.Signals.Count == 0)
        {
            sb.AppendLine("None.");
        }
        else
        {
            foreach (var f in result.Signals.Take(o.MaxRows))
            {
                sb.Append("- **").Append(f.Category).Append("** — ");
                if (!string.IsNullOrWhiteSpace(f.MatchedText))
                    sb.Append('“').Append(f.MatchedText.Trim()).Append("” — ");
                sb.Append(f.Message).Append(' ').Append("*→ ").Append(f.Suggestion).AppendLine("*");
            }
            if (result.Signals.Count > o.MaxRows)
                sb.Append("\n… and ").Append(result.Signals.Count - o.MaxRows).AppendLine(" more.");
        }
        sb.AppendLine();

        if (result.Observations.Count > 0)
        {
            sb.AppendLine("## Found, but at a rate people write at");
            sb.AppendLine();
            sb.AppendLine("Measured against writing published before generative models existed. Shown " +
                          "because they are real, and counted for nothing because they are ordinary.");
            sb.AppendLine();
            foreach (var group in result.Observations.GroupBy(f => f.RuleId).Take(o.MaxRows))
                sb.Append("- ").Append(group.Key).Append(" — ").Append(group.Count())
                  .Append(group.Count() == 1 ? " occurrence" : " occurrences").AppendLine();
            sb.AppendLine();
        }

        sb.AppendLine("## How often this is wrong");
        sb.AppendLine();
        sb.AppendLine(HowOftenWrong());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This report was produced on the device that ran the analysis and contains " +
                      "material from the document it describes. It is yours to keep or to send; nothing " +
                      "here was uploaded anywhere.*");

        return sb.ToString();
    }

    /// <summary>
    /// The same report as a self-contained HTML page — no stylesheet, no script, no external request —
    /// so it opens anywhere by double-click and prints to PDF from the browser. Chosen over generating
    /// a PDF directly because a PDF writer is a dependency, and this library has none, which is what
    /// lets it run inside a browser tab.
    /// </summary>
    public static string ToHtml(AnalysisResult result, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var body = MarkdownToHtml(ToMarkdown(result, o));

        return $"""
            <!doctype html>
            <html lang="{(result.Language == "es" ? "es" : "en")}">
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Escape(o.Title)}</title>
            <style>
            {Css}
            </style>
            {body}
            </html>
            """;
    }

    /// <summary>
    /// A whole folder as one document: every file scanned, ordered worst first, over the same caveat
    /// every single-document report carries.
    ///
    /// A stack of essays is what a teacher actually has, and the honest thing a triage list can do is
    /// tell them where to <em>look</em> — not which student to accuse. So the top of the list is the
    /// reading order, and the page says so, because a table sorted by score with no explanation reads
    /// like a ranking of guilt.
    /// </summary>
    public static string FolderToMarkdown(
        string folderName, IReadOnlyList<FolderEntry> entries, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        var sb = new StringBuilder();
        var scored = entries.Where(e => e.Score is not null).ToList();
        var unreadable = entries.Where(e => e.Error is not null).ToList();

        sb.Append("# ").AppendLine(o.Title);
        sb.AppendLine();
        sb.Append("**Folder:** ").AppendLine(folderName);
        sb.Append("**Generated:** ").Append(o.GeneratedOn).Append(" · **Engine:** SignsOfAI ")
          .AppendLine(o.EngineVersion);
        sb.AppendLine();
        sb.Append(entries.Count).Append(" file").Append(entries.Count == 1 ? "" : "s").Append(" scanned");
        if (unreadable.Count > 0) sb.Append(", ").Append(unreadable.Count).Append(" unreadable");
        sb.AppendLine(".");
        sb.AppendLine();
        sb.AppendLine(Caveat());
        sb.AppendLine();
        sb.AppendLine("> **This is a reading order, not a ranking.** A higher score means look sooner, " +
                      "and nothing more. Nothing on this page establishes that anyone did anything.");
        sb.AppendLine();

        sb.AppendLine("| File | Score | Signals | Words |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var e in scored.OrderByDescending(e => e.Score).ThenBy(e => e.Name).Take(o.MaxRows))
            sb.Append("| ").Append(e.Name).Append(" | ").Append(Num(e.Score ?? 0, 0))
              .Append(" | ").Append(e.Signals?.ToString(CultureInfo.InvariantCulture) ?? "—")
              .Append(" | ").Append(e.Words?.ToString(CultureInfo.InvariantCulture) ?? "—")
              .AppendLine(" |");
        if (scored.Count > o.MaxRows)
            sb.AppendLine().Append("… and ").Append(scored.Count - o.MaxRows).AppendLine(" more.");
        sb.AppendLine();

        if (unreadable.Count > 0)
        {
            sb.AppendLine("## Could not be read");
            sb.AppendLine();
            foreach (var e in unreadable.Take(o.MaxRows))
                sb.Append("- ").Append(e.Name).Append(" — ").AppendLine(e.Error);
            sb.AppendLine();
        }

        sb.AppendLine("## How often this is wrong");
        sb.AppendLine();
        sb.AppendLine(HowOftenWrong());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Produced on the device that scanned the folder. It names your students' files, " +
                      "so treat it as you would the coursework itself; nothing here was uploaded anywhere.*");

        return sb.ToString();
    }

    /// <inheritdoc cref="FolderToMarkdown"/>
    public static string FolderToHtml(
        string folderName, IReadOnlyList<FolderEntry> entries, ReportOptions? options = null)
    {
        var o = options ?? ReportOptions.Default;
        return $"""
            <!doctype html>
            <html lang="en">
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Escape(o.Title)}</title>
            <style>
            {Css}
            </style>
            {MarkdownToHtml(FolderToMarkdown(folderName, entries, o))}
            </html>
            """;
    }

    /// <summary>
    /// The sentence that has to appear on every report. Written from the embedded calibration so it
    /// cannot go stale, and explicit when there is none: a fork that has not measured itself says so
    /// rather than inheriting a number it did not earn.
    /// </summary>
    private static string Caveat()
    {
        var c = PublishedCalibration.Current;
        if (c?.RecommendedThreshold is not { } threshold)
            return "> **This build has not been calibrated.** No false-positive rate has been measured " +
                   "for it, so the score above should not be used to support a decision about a person.";

        return $"> **A score is not proof.** This build flags at most {Pct(c.RateHigh)} of writing known " +
               $"to be human at a threshold of {Num(threshold)}/100 — the upper bound of a 95% interval, " +
               $"not the observed rate. Below that threshold, treat the score as saying nothing.";
    }

    private static string HowOftenWrong()
    {
        var c = PublishedCalibration.Current;
        if (c is null)
            return "This build ships no calibration, so nothing is known about how often it is wrong. " +
                   "That is itself the most important thing on this page.";

        var sb = new StringBuilder();
        sb.Append("Measured against **").Append(c.Texts)
          .Append(" texts published before generative models existed**, so their authorship rests on ")
          .Append("their dates rather than on anybody's judgement. Measured on ").Append(c.MeasuredOn)
          .Append(" with engine ").Append(c.Engine).AppendLine(".");
        sb.AppendLine();

        if (c.RecommendedThreshold is { } threshold)
        {
            sb.Append("At **").Append(Num(threshold)).Append("/100**, ").Append(c.FlaggedAtThreshold)
              .Append(" of those ").Append(c.Texts).Append(" were flagged — an observed ")
              .Append(Pct((double)c.FlaggedAtThreshold / Math.Max(c.Texts, 1)))
              .Append(", with a 95% interval of ").Append(Pct(c.RateLow)).Append(" – ")
              .Append(Pct(c.RateHigh)).AppendLine(".");
            sb.AppendLine();
            sb.AppendLine("Read the interval, not the observed rate. Nought out of ninety is not a " +
                          "false-positive rate of zero.");
        }

        if (c.NoisiestRules.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("The rules seen most often on that human writing, worst first — if the " +
                          "evidence above leans on one of these, weigh it accordingly:");
            sb.AppendLine();
            foreach (var rule in c.NoisiestRules)
                sb.Append("- `").Append(rule.RuleId).Append("` — ").Append(Pct(rule.TextShare))
                  .AppendLine(" of human texts");
        }

        sb.AppendLine();
        sb.AppendLine("What this does **not** tell you: how much machine-written text it catches. That " +
                      "is the other half of the picture and it is deliberately not measured here, because " +
                      "any collection of machine-written text samples whichever models were convenient " +
                      "that month. A tool that flags nothing has a perfect false-positive rate.");

        return sb.ToString();
    }


    /// <summary>
    /// Inlined rather than linked: the page has to open from a downloads folder years later, on a
    /// machine that has never heard of this project, with no network. Serif on purpose — this is a
    /// document to be read and printed, not an interface.
    /// </summary>
    private const string Css = """
        :root { color-scheme: light dark; }
        body { font: 16px/1.65 ui-serif, Georgia, "Times New Roman", serif;
               max-width: 46rem; margin: 3rem auto; padding: 0 1.2rem; }
        h1 { font-size: 1.7rem; line-height: 1.25; }
        h2 { font-size: 1.15rem; margin-top: 2.2rem; border-bottom: 1px solid #8884;
             padding-bottom: .3rem; }
        h3 { font-size: 1rem; margin-top: 1.6rem; }
        table { border-collapse: collapse; width: 100%; font-size: .92rem; }
        th, td { text-align: left; padding: .35rem .6rem; border-bottom: 1px solid #8883; }
        td:nth-child(n+3), th:nth-child(n+3) { text-align: right; }
        blockquote { margin: 1rem 0; padding: .1rem 1rem; border-left: 3px solid #8886; opacity: .85; }
        code { font-family: ui-monospace, Consolas, monospace; font-size: .9em; }
        hr { border: 0; border-top: 1px solid #8884; margin: 2rem 0; }
        @media print { body { margin: 0; max-width: none; } }
        """;

    private static string Describe(ArtifactKind kind) => kind switch
    {
        ArtifactKind.InvisibleCharacter => "Invisible character",
        ArtifactKind.BidiControl => "Text-direction control",
        ArtifactKind.LookalikeLetter => "Letter from another alphabet",
        ArtifactKind.UnusualSpace => "Unusual space",
        ArtifactKind.SoftHyphen => "Soft hyphen",
        ArtifactKind.VariationSelector => "Variation selector",
        ArtifactKind.PrivateUse => "Private-use character",
        ArtifactKind.TagCharacter => "Tag character (invisible)",
        _ => kind.ToString(),
    };

    private static string Num(double value, int decimals = 1) =>
        Math.Round(value, decimals).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Pct(double fraction) =>
        (fraction * 100).ToString(fraction is > 0 and < 0.1 ? "0.0" : "0.#",
                                  CultureInfo.InvariantCulture) + "%";

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Converts exactly the Markdown this file emits and nothing else. Deliberately not a general
    /// converter: the input is produced twenty lines up, so anything it cannot handle is a bug here
    /// rather than a user's document, and a full parser would be a dependency this library refuses.
    /// </summary>
    private static string MarkdownToHtml(string markdown)
    {
        var html = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        bool inTable = false, inList = false;

        void CloseBlocks()
        {
            if (inTable) { html.AppendLine("</tbody></table>"); inTable = false; }
            if (inList) { html.AppendLine("</ul>"); inList = false; }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("| ", StringComparison.Ordinal))
            {
                var cells = line.Trim('|').Split('|').Select(c => Inline(c.Trim())).ToList();
                if (!inTable)
                {
                    // The row after the header is the alignment row; it carries no content.
                    if (i + 1 < lines.Length && lines[i + 1].StartsWith("|---", StringComparison.Ordinal))
                    {
                        CloseBlocks();
                        html.Append("<table><thead><tr>");
                        foreach (var cell in cells) html.Append("<th>").Append(cell).Append("</th>");
                        html.AppendLine("</tr></thead><tbody>");
                        inTable = true;
                        i++;
                        continue;
                    }
                }
                else
                {
                    html.Append("<tr>");
                    foreach (var cell in cells) html.Append("<td>").Append(cell).Append("</td>");
                    html.AppendLine("</tr>");
                    continue;
                }
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (inTable) { html.AppendLine("</tbody></table>"); inTable = false; }
                if (!inList) { html.AppendLine("<ul>"); inList = true; }
                html.Append("<li>").Append(Inline(line[2..])).AppendLine("</li>");
                continue;
            }

            CloseBlocks();

            if (line.StartsWith("### ", StringComparison.Ordinal))
                html.Append("<h3>").Append(Inline(line[4..])).AppendLine("</h3>");
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                html.Append("<h2>").Append(Inline(line[3..])).AppendLine("</h2>");
            else if (line.StartsWith("# ", StringComparison.Ordinal))
                html.Append("<h1>").Append(Inline(line[2..])).AppendLine("</h1>");
            else if (line.StartsWith("> ", StringComparison.Ordinal))
                html.Append("<blockquote>").Append(Inline(line[2..])).AppendLine("</blockquote>");
            else if (line.StartsWith("---", StringComparison.Ordinal))
                html.AppendLine("<hr>");
            else if (line.Length > 0)
                html.Append("<p>").Append(Inline(line)).AppendLine("</p>");
        }

        CloseBlocks();
        return html.ToString();
    }

    /// <summary>Bold, italic and code, applied after escaping so a document cannot inject markup.</summary>
    private static string Inline(string text)
    {
        var s = Escape(text);
        s = Wrap(s, "**", "<strong>", "</strong>");
        s = Wrap(s, "`", "<code>", "</code>");
        s = Wrap(s, "*", "<em>", "</em>");
        return s;
    }

    private static string Wrap(string text, string marker, string open, string close)
    {
        var parts = text.Split(marker);
        if (parts.Length < 3) return text;

        var sb = new StringBuilder(parts[0]);
        for (int i = 1; i < parts.Length; i++)
            sb.Append(i % 2 == 1 ? open : close).Append(parts[i]);

        // An odd number of markers means one is unmatched; leaving a tag open would break the page.
        if (parts.Length % 2 == 0) sb.Append(close);
        return sb.ToString();
    }
}

/// <summary>How a report is labelled. Everything has a default so a caller can pass nothing.</summary>
public sealed record ReportOptions
{
    public string Title { get; init; } = "Writing analysis report";

    /// <summary>The file or assignment this describes. Blank when the text was pasted.</summary>
    public string DocumentName { get; init; } = "";

    public string GeneratedOn { get; init; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    public string EngineVersion { get; init; } =
        typeof(EvidenceReport).Assembly.GetName().Version?.ToString(3) ?? "";

    /// <summary>
    /// Where each list stops. A report meant to be read by a person is worth less at four hundred rows
    /// than at forty, and the count of what was left out is printed rather than the rows themselves.
    /// </summary>
    public int MaxRows { get; init; } = 40;

    public static ReportOptions Default { get; } = new();
}

/// <summary>One file in a folder scan, as the report needs it.</summary>
public sealed record FolderEntry(
    string Name, int? Words, double? Score, int? Signals, string? Error);

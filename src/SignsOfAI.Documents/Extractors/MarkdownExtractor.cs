using System.Text;
using System.Text.RegularExpressions;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts prose from Markdown files (.md). Strips formatting syntax — headers, emphasis,
/// links, images, code blocks, blockquotes, horizontal rules, and HTML tags — so the
/// analyser sees the prose the reader would read, not the markup.
/// </summary>
public sealed partial class MarkdownExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd", ".mkdn", ".rmd", ".qmd",
    };

    public bool CanHandle(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName));

    public async Task<ExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (options.MaxSizeBytes is { } max && bytes.Length > max)
        {
            return BuildFailure(fileName, ExtractionFailureReason.FileTooLarge,
                $"File is {bytes.Length:N0} bytes; limit is {max:N0} bytes.", bytes.Length);
        }

        var rawText = TxtExtractor.Decode(bytes, out _);
        var prose = StripMarkdown(rawText);
        var paragraphs = TxtExtractor.SplitParagraphs(prose);

        return new ExtractionResult
        {
            Text = prose,
            Paragraphs = paragraphs,
            Warnings = [],
            FileName = fileName,
            BytesRead = bytes.Length,
        };
    }

    /// <summary>
    /// Strips Markdown formatting, leaving only the prose. The order of operations matters:
    /// fenced code blocks are removed first so their content isn't misinterpreted as
    /// formatting, then images (which look like links with a <c>!</c>), then links (keeping
    /// the display text), then headers, emphasis markers, blockquotes, list markers,
    /// horizontal rules, and finally inline HTML.
    /// </summary>
    internal static string StripMarkdown(string text)
    {
        // Remove fenced code blocks (``` ... ```) — these aren't prose.
        text = FencedCodeBlockRegex().Replace(text, "");

        // Remove images: ![alt](url) → nothing (the alt text is for accessibility, not the prose)
        text = ImageRegex().Replace(text, "");

        // Convert links: [text](url) → text
        text = LinkRegex().Replace(text, "$1");

        // Remove reference-style links: [text][ref] or [text][] → text
        text = RefLinkRegex().Replace(text, "$1");

        // Remove headers: the # markers, keep the header text
        text = HeaderRegex().Replace(text, "$1");

        // Remove bold/italic markers: **, __, *, _
        // Each alternative captures its content in groups 1–4; only one is non-empty at a time.
        text = BoldItalicRegex().Replace(text, "$1$2$3$4");

        // Remove strikethrough: ~~text~~ → text
        text = StrikethroughRegex().Replace(text, "$1");

        // Remove blockquote markers (> at line start)
        text = BlockquoteRegex().Replace(text, "$1");

        // Remove unordered list markers (-, *, + at line start)
        text = UnorderedListRegex().Replace(text, "$1");

        // Remove ordered list markers (1., 2) at line start)
        text = OrderedListRegex().Replace(text, "$1");

        // Remove horizontal rules (---, ***, ___)
        text = HorizontalRuleRegex().Replace(text, "");

        // Remove inline HTML tags
        text = HtmlTagRegex().Replace(text, "");

        // Remove HTML comments <!-- ... -->
        text = HtmlCommentRegex().Replace(text, "");

        return text.Trim();
    }

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Multiline)]
    private static partial Regex FencedCodeBlockRegex();

    [GeneratedRegex(@"!\[.*?\]\(.*?\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^\)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\[([^\]]*)\](?:\s*\[[^\]]*\])?")]
    private static partial Regex RefLinkRegex();

    [GeneratedRegex(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__|\*(.+?)\*|_(.+?)_")]
    private static partial Regex BoldItalicRegex();

    [GeneratedRegex(@"~~(.+?)~~")]
    private static partial Regex StrikethroughRegex();

    [GeneratedRegex(@"^>\s?(.*)$", RegexOptions.Multiline)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex(@"^[\-\*\+]\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex UnorderedListRegex();

    [GeneratedRegex(@"^\d+[\.\)]\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex OrderedListRegex();

    [GeneratedRegex(@"^[\-_\*]{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex HtmlCommentRegex();

    private static ExtractionResult BuildFailure(
        string fileName, ExtractionFailureReason reason, string message, long bytesRead)
    {
        return new ExtractionResult
        {
            Text = "",
            Paragraphs = [],
            Warnings = [new ExtractionWarning($"[{reason}] {message}", null)],
            FileName = fileName,
            BytesRead = bytesRead,
        };
    }
}

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts text from OpenDocument Text (.odt) files. An .odt is a ZIP archive whose body
/// is <c>content.xml</c> — same trick as DOCX, different XML schema. No external dependency
/// needed: <see cref="ZipArchive"/> and LINQ-to-XML are enough.
/// </summary>
public sealed class OdtExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".odt",
    };

    // ODF 1.2 namespaces
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

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

        try
        {
            ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            // ODF spec mandates forward slashes, but be lenient with backslashes.
            var entry = zip.GetEntry("content.xml")
                ?? zip.Entries.FirstOrDefault(e =>
                       e.FullName.Replace('\\', '/').Equals("content.xml", StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                    "This doesn't look like an .odt file (no content.xml).", bytes.Length);
            }

            using var entryStream = entry.Open();
            var doc = await XDocument.LoadAsync(entryStream, LoadOptions.None, ct);

            var sb = new StringBuilder();
            // <office:document-content> → <office:body> → <office:text> → <text:p> / <text:h>
            var body = doc.Element(Office + "document-content")?.Element(Office + "body");
            var text = body?.Element(Office + "text");

            if (text is null)
            {
                return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                    "The content.xml has no office:text element — unexpected ODF structure.", bytes.Length);
            }

            // Walk text:p and text:h in document order
            foreach (var element in text.Elements())
            {
                if (element.Name == TextNs + "p" || element.Name == TextNs + "h")
                {
                    sb.Append(ExtractTextFromElement(element));
                }
                // text:section, text:list, etc. — dive in
                else if (element.Name.Namespace == TextNs)
                {
                    foreach (var p in element.Descendants(TextNs + "p"))
                        sb.Append(ExtractTextFromElement(p));
                    foreach (var h in element.Descendants(TextNs + "h"))
                        sb.Append(ExtractTextFromElement(h));
                }
            }

            var rawText = sb.ToString().Trim();
            // ODT paragraphs are separated by single newlines from the element loop;
            // build paragraph spans directly from each non-empty line.
            var paragraphs = BuildParagraphsFromLines(rawText);

            return new ExtractionResult
            {
                Text = rawText,
                Paragraphs = paragraphs,
                Warnings = [],
                FileName = fileName,
                BytesRead = bytes.Length,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                $"Failed to parse .odt: {ex.Message}", bytes.Length);
        }
    }

    /// <summary>
    /// Extracts text from a text:p or text:h element, walking direct child nodes
    /// (text:span, text:a, text nodes, tab, line-break, text:s) in document order.
    /// Tabs and line breaks are mapped to their plain-text equivalents.
    /// </summary>
    private static string ExtractTextFromElement(XElement element)
    {
        var sb = new StringBuilder();

        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText textNode:
                    sb.Append(textNode.Value);
                    break;

                case XElement child:
                    switch (child.Name.LocalName)
                    {
                        case "tab":
                            sb.Append('\t');
                            break;
                        case "line-break":
                            sb.Append('\n');
                            break;
                        case "s" when child.Name.Namespace == TextNs:
                            // text:s — a run of non-breaking spaces; @text:c gives the count
                            var count = (int?)child.Attribute(TextNs + "c") ?? 1;
                            sb.Append(new string(' ', count));
                            break;
                        case "span":
                        case "a":
                            // Dive into the span/hyperlink to get its text content
                            sb.Append(child.Value);
                            break;
                        default:
                            // Unknown child — grab its text value anyway
                            sb.Append(child.Value);
                            break;
                    }
                    break;
            }
        }

        // If we got nothing from child nodes, fall back to the element's own text value
        if (sb.Length == 0 && !string.IsNullOrEmpty(element.Value))
            sb.Append(element.Value);

        sb.Append('\n'); // end of paragraph
        return sb.ToString();
    }

    private static IReadOnlyList<ParagraphSpan> BuildParagraphsFromLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraphs = new List<ParagraphSpan>();
        var index = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                paragraphs.Add(new ParagraphSpan(index++, trimmed, null));
        }
        return paragraphs;
    }

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

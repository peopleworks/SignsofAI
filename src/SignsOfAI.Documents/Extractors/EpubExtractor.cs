using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts text from EPUB (.epub) files. An EPUB is a ZIP archive; we read the OPF spine
/// for the reading order, then extract prose from each XHTML chapter in order — all with
/// no external dependency.
/// </summary>
public sealed partial class EpubExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".epub",
    };

    // OPF namespace (EPUB 2 and 3)
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    // Container namespace
    private static readonly XNamespace Cnt = "urn:oasis:names:tc:opendocument:xmlns:container";

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

            // Step 1: find the OPF file via META-INF/container.xml
            var opfPath = FindOpfPath(zip);
            if (opfPath is null)
            {
                return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                    "No META-INF/container.xml or missing rootfile entry.", bytes.Length);
            }

            // Step 2: parse the OPF to get the spine (reading order)
            var opfEntry = FindEntryLenient(zip, opfPath);
            if (opfEntry is null)
            {
                return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                    $"The OPF file '{opfPath}' referenced by container.xml was not found in the ZIP.",
                    bytes.Length);
            }

            using var opfStream = opfEntry.Open();
            var opfDoc = await XDocument.LoadAsync(opfStream, LoadOptions.None, ct);
            var opfRoot = opfDoc.Root;
            if (opfRoot is null)
            {
                return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                    "The OPF file is empty.", bytes.Length);
            }

            // Build manifest: id → href
            var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
            var manifestEl = opfRoot.Element(Opf + "manifest");
            if (manifestEl is not null)
            {
                foreach (var item in manifestEl.Elements(Opf + "item"))
                {
                    var id = (string?)item.Attribute("id");
                    var href = (string?)item.Attribute("href");
                    if (id is not null && href is not null)
                        manifest[id] = href;
                }
            }

            // Read the spine for ordered chapter references
            var spine = opfRoot.Element(Opf + "spine");
            if (spine is null)
            {
                return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                    "The OPF file has no spine.", bytes.Length);
            }

            var opfDirectory = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? "";

            var sb = new StringBuilder();
            var warnings = new List<ExtractionWarning>();

            foreach (var itemref in spine.Elements(Opf + "itemref"))
            {
                var idref = (string?)itemref.Attribute("idref");
                if (idref is null || !manifest.TryGetValue(idref, out var href))
                    continue;

                // Resolve relative path
                var chapterPath = string.IsNullOrEmpty(opfDirectory)
                    ? href
                    : $"{opfDirectory}/{href}";

                var chapterEntry = FindEntryLenient(zip, chapterPath);
                if (chapterEntry is null)
                {
                    warnings.Add(new ExtractionWarning(
                        $"Chapter '{chapterPath}' referenced in spine was not found in the ZIP.", null));
                    continue;
                }

                using var chapterStream = chapterEntry.Open();
                var chapterDoc = await XDocument.LoadAsync(chapterStream, LoadOptions.None, ct);

                // Extract text from XHTML body
                var body = chapterDoc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName.Equals("body", StringComparison.OrdinalIgnoreCase));

                if (body is not null)
                {
                    var chapterText = StripHtml(body);
                    if (!string.IsNullOrWhiteSpace(chapterText))
                        sb.AppendLine(chapterText);
                }
            }

            var rawText = sb.ToString().Trim();
            var paragraphs = TxtExtractor.SplitParagraphs(rawText);

            return new ExtractionResult
            {
                Text = rawText,
                Paragraphs = paragraphs,
                Warnings = warnings,
                FileName = fileName,
                BytesRead = bytes.Length,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuildFailure(fileName, ExtractionFailureReason.UnexpectedError,
                $"Failed to parse EPUB: {ex.Message}", bytes.Length);
        }
    }

    /// <summary>
    /// Reads META-INF/container.xml to find the path to the OPF file.
    /// </summary>
    private static string? FindOpfPath(ZipArchive zip)
    {
        var containerEntry = FindEntryLenient(zip, "META-INF/container.xml");
        if (containerEntry is null)
            return null;

        using var stream = containerEntry.Open();
        var doc = XDocument.Load(stream);
        var rootfile = doc.Descendants(Cnt + "rootfile")
            .FirstOrDefault();

        return (string?)rootfile?.Attribute("full-path");
    }

    /// <summary>
    /// Finds a ZIP entry by path, normalising backslashes to forward slashes for lenient matching.
    /// </summary>
    private static ZipArchiveEntry? FindEntryLenient(ZipArchive zip, string path)
    {
        var normalised = path.Replace('\\', '/');
        return zip.GetEntry(normalised)
            ?? zip.Entries.FirstOrDefault(e =>
                   e.FullName.Replace('\\', '/').Equals(normalised, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Strips HTML tags from an XElement, preserving text content and adding newlines for
    /// block-level elements so paragraphs stay separated.
    /// </summary>
    private static string StripHtml(XElement element)
    {
        // Render to string first, then strip tags with some block-element handling
        var html = element.ToString(SaveOptions.DisableFormatting);

        // Replace block-level elements with newlines so paragraphs separate
        var blocksReplaced = BlockElementRegex().Replace(html, "\n$0");
        blocksReplaced = BlockCloseRegex().Replace(blocksReplaced, "\n");

        // Remove all remaining tags
        var text = HtmlTagStripRegex().Replace(blocksReplaced, "");

        // Decode common HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);

        // Collapse multiple blank lines
        text = MultiNewlineRegex().Replace(text, "\n\n");

        return text.Trim();
    }

    [GeneratedRegex(@"<(?:p|div|h[1-6]|li|tr|br|hr|section|article|header|footer|main|aside|nav|figure|figcaption|blockquote|pre|table|ul|ol|dl)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockElementRegex();

    [GeneratedRegex(@"</(?:p|div|h[1-6]|li|tr|section|article|header|footer|main|aside|nav|figure|figcaption|blockquote|pre|table|ul|ol|dl)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockCloseRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagStripRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiNewlineRegex();

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

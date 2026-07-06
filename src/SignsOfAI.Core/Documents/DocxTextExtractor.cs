using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SignsOfAI.Core.Documents;

/// <summary>
/// Extracts plain text from a Word .docx file. A .docx is an OPC (ZIP) package whose body lives in
/// <c>word/document.xml</c>; we read that entry and pull the WordprocessingML runs — all with the
/// BCL's <see cref="ZipArchive"/> and LINQ-to-XML, so it works client-side in Blazor WebAssembly
/// with no external dependency.
/// </summary>
public static class DocxTextExtractor
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static async Task<string> ExtractTextAsync(Stream stream, CancellationToken ct = default)
    {
        // ZipArchive needs a seekable stream; browser file streams aren't, so buffer first.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        // The OPC spec mandates forward slashes, but some ZIP writers emit backslashes — match leniently.
        var entry = zip.GetEntry("word/document.xml")
            ?? zip.Entries.FirstOrDefault(e =>
                   e.FullName.Replace('\\', '/').Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("This doesn't look like a .docx file (no word/document.xml).");

        using var entryStream = entry.Open();
        var doc = await XDocument.LoadAsync(entryStream, LoadOptions.None, ct);

        var sb = new StringBuilder();
        foreach (var paragraph in doc.Descendants(W + "p"))
        {
            // Walk the paragraph's descendants in document order so text, tabs and line breaks interleave correctly.
            foreach (var node in paragraph.Descendants())
            {
                switch (node.Name.LocalName)
                {
                    case "t": sb.Append(node.Value); break;      // <w:t> — a text run
                    case "tab": sb.Append('\t'); break;          // <w:tab>
                    case "br":
                    case "cr": sb.Append('\n'); break;           // <w:br> / <w:cr> — soft line break
                }
            }
            sb.Append('\n'); // end of paragraph
        }

        return sb.ToString().Trim();
    }
}

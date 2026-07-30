using System.IO.Compression;
using System.Text;

namespace SignsOfAI.Documents.Tests;

/// <summary>
/// Generates fixture files in-memory so tests need no external files and run in CI with no network.
/// Every fixture is kept under ~50 KB.
/// </summary>
public static class TestFixtures
{
    // ── TXT ────────────────────────────────────────────────────────

    public static byte[] Utf8Txt()
    {
        return Encoding.UTF8.GetBytes("Hello world.\n\nThis is paragraph two.\n\nGoodbye.");
    }

    public static byte[] Utf16LeTxt()
    {
        // BOM + UTF-16 LE content
        var bom = new byte[] { 0xFF, 0xFE };
        var content = Encoding.Unicode.GetBytes("Hello with BOM.\n\nSecond paragraph.");
        return [..bom, ..content];
    }

    public static byte[] Utf8BomTxt()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = Encoding.UTF8.GetBytes("Hello with UTF-8 BOM.\n\nAnother paragraph.");
        return [..bom, ..content];
    }

    public static byte[] Latin1Txt()
    {
        return Encoding.GetEncoding(28591).GetBytes("Café résumé naïve.\n\nSecond line with ñ and ü.");
    }

    // ── Markdown ───────────────────────────────────────────────────

    public static byte[] MarkdownFile()
    {
        var md = """
            # Heading 1

            This is a **bold** paragraph with *italic* text and a [link](https://example.com).

            ## Heading 2

            Here is an image: ![alt](img.png) which should be removed.

            - List item one
            - List item two
            - List item three

            1. Ordered first
            2. Ordered second

            > This is a blockquote with **bold** inside.

            ```csharp
            // This code block should be removed entirely
            var x = 1;
            ```

            Plain paragraph after code block.

            ---

            Final paragraph with ~~strikethrough~~ text.
            """;

        return Encoding.UTF8.GetBytes(md);
    }

    // ── DOCX ───────────────────────────────────────────────────────

    private const string DocxXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:body>
            <w:p><w:r><w:t>First paragraph of the DOCX file.</w:t></w:r></w:p>
            <w:p><w:r><w:t>Second paragraph</w:t></w:r><w:r><w:br/></w:r><w:r><w:t>with a line break inside.</w:t></w:r></w:p>
            <w:p><w:r><w:t>Third paragraph with a</w:t></w:r><w:r><w:tab/></w:r><w:r><w:t>tab character.</w:t></w:r></w:p>
          </w:body>
        </w:document>
        """;

    public static byte[] DocxFile() => BuildZip(new Dictionary<string, string>
    {
        ["word/document.xml"] = DocxXml,
    });

    // ── ODT ────────────────────────────────────────────────────────

    private const string OdtContentXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <office:document-content
          xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
          xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
          <office:body>
            <office:text>
              <text:p>First paragraph of the ODT.</text:p>
              <text:p>Second paragraph with a <text:span>span</text:span> inside.</text:p>
              <text:h>This is a heading paragraph.</text:h>
              <text:p>Final paragraph.</text:p>
            </office:text>
          </office:body>
        </office:document-content>
        """;

    public static byte[] OdtFile() => BuildZip(new Dictionary<string, string>
    {
        ["content.xml"] = OdtContentXml,
        // ODF also requires a mimetype file but we're lenient
    });

    // ── EPUB ───────────────────────────────────────────────────────

    private const string ContainerXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private const string ContentOpf = """
        <?xml version="1.0" encoding="UTF-8"?>
        <package xmlns="http://www.idpf.org/2007/opf" version="2.0">
          <manifest>
            <item id="chap1" href="chapter1.xhtml" media-type="application/xhtml+xml"/>
            <item id="chap2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>
            <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
          </manifest>
          <spine>
            <itemref idref="chap1"/>
            <itemref idref="chap2"/>
          </spine>
        </package>
        """;

    private const string Chapter1Xhtml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml">
        <head><title>Chapter 1</title></head>
        <body>
          <h1>Chapter One</h1>
          <p>This is the first paragraph of chapter one.</p>
          <p>This is the second paragraph, with <em>emphasis</em> and <strong>strong</strong> text.</p>
        </body>
        </html>
        """;

    private const string Chapter2Xhtml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml">
        <head><title>Chapter 2</title></head>
        <body>
          <h1>Chapter Two</h1>
          <p>This is the first paragraph of chapter two.</p>
          <p>Final paragraph of the book.</p>
        </body>
        </html>
        """;

    public static byte[] EpubFile() => BuildZip(new Dictionary<string, string>
    {
        ["META-INF/container.xml"] = ContainerXml,
        ["OEBPS/content.opf"] = ContentOpf,
        ["OEBPS/chapter1.xhtml"] = Chapter1Xhtml,
        ["OEBPS/chapter2.xhtml"] = Chapter2Xhtml,
    });

    // ── RTF ────────────────────────────────────────────────────────

    private const string RtfContent = """
        {\rtf1\ansi\deff0
        {\fonttbl{\f0\fswiss Helvetica;}}
        \f0
        First paragraph of RTF.\par
        Second paragraph with \b bold\b0 and \i italic\i0 text.\par
        \pard Third paragraph after a \b bold heading\b0 .\par
        }
        """;

    public static byte[] RtfFile() => Encoding.ASCII.GetBytes(RtfContent);

    // ── PDF ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a minimal valid PDF with two pages of text.
    /// Hand-crafted so tests run with no external PDF writer library.
    /// </summary>
    public static byte[] TwoPagePdf()
    {
        // Font object (Helvetica, Type 1)
        const string fontObj = "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n";

        // Page 1 content stream: "Page One Content.\nMore text."
        var page1Text = "BT /F1 12 Tf 72 700 Td (Page One Content.) Tj T* (More text on page one.) Tj ET";
        var page1Stream = "<< /Length " + (page1Text.Length + 2) + " >>\nstream\n" + page1Text + "\nendstream\n";
        var page1ContentObj = "6 0 obj\n" + page1Stream + "endobj\n";

        // Page 2 content stream: "Page Two Content.\nDifferent text."
        var page2Text = "BT /F1 12 Tf 72 700 Td (Page Two Content.) Tj T* (Different text on page two.) Tj ET";
        var page2Stream = "<< /Length " + (page2Text.Length + 2) + " >>\nstream\n" + page2Text + "\nendstream\n";
        var page2ContentObj = "7 0 obj\n" + page2Stream + "endobj\n";

        // Page objects
        const string page1Obj = "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 6 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n";
        const string page2Obj = "4 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 7 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n";

        // Pages object (parent of all pages)
        const string pagesObj = "2 0 obj\n<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>\nendobj\n";

        // Catalog
        const string catalogObj = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";

        // Assemble the body
        var body = catalogObj + pagesObj + page1Obj + page2Obj + fontObj + page1ContentObj + page2ContentObj;

        // Build xref table
        // Offsets: we need to compute byte positions of each object
        var header = "%PDF-1.4\n%âãÏÓ\n";

        // Compute offsets
        var offsets = new Dictionary<int, long>();
        var pos = (long)header.Length;

        // Find each "N 0 obj" in the body and record its offset
        for (int i = 1; i <= 7; i++)
        {
            offsets[i] = pos;
            var marker = $"{i} 0 obj\n";
            var idx = body.IndexOf(marker, StringComparison.Ordinal);
            // Move past this object
            var endMarker = "endobj\n";
            var endIdx = body.IndexOf(endMarker, idx + marker.Length, StringComparison.Ordinal);
            pos += endIdx + endMarker.Length;
        }

        // Build xref
        var xref = new StringBuilder();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {offsets.Count + 1}");
        xref.AppendLine("0000000000 65535 f ");
        foreach (var kv in offsets.OrderBy(k => k.Key))
        {
            xref.AppendLine($"{kv.Value:0000000000} 00000 n ");
        }

        var trailer = "trailer\n<< /Size " + (offsets.Count + 1) + " /Root 1 0 R >>\nstartxref\n" + pos + "\n%%EOF\n";

        var pdfBytes = Encoding.ASCII.GetBytes(header + body + xref + trailer);
        return pdfBytes;
    }

    // ── Broken / edge-case fixtures ────────────────────────────────

    public static byte[] ZeroByteFile() => [];

    public static byte[] TruncatedPdf()
    {
        var full = TwoPagePdf();
        return full[..(full.Length / 2)];
    }

    public static byte[] NonDocxZip()
    {
        return BuildZip(new Dictionary<string, string>
        {
            ["readme.txt"] = "This is just a regular ZIP, not a document.",
        });
    }

    public static byte[] CorruptOdt()
    {
        // A ZIP that has content.xml but it's not valid ODF XML
        return BuildZip(new Dictionary<string, string>
        {
            ["content.xml"] = "<not-odf>broken</not-odf>",
        });
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static byte[] BuildZip(Dictionary<string, string> entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        return ms.ToArray();
    }

    public static MemoryStream Stream(byte[] bytes)
    {
        var ms = new MemoryStream(bytes);
        return ms;
    }
}

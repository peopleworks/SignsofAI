using System.IO.Compression;
using System.Text;
using SignsOfAI.Core.Documents;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class DocxTextExtractorTests
{
    private const string DocXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:body>
            <w:p><w:r><w:t>Hello</w:t></w:r><w:r><w:t xml:space="preserve"> world</w:t></w:r></w:p>
            <w:p><w:r><w:t>Second</w:t></w:r><w:r><w:br/></w:r><w:r><w:t>line</w:t></w:r></w:p>
          </w:body>
        </w:document>
        """;

    private static MemoryStream BuildDocx(string documentXml)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(documentXml);
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Extracts_text_runs_paragraphs_and_breaks()
    {
        using var docx = BuildDocx(DocXml);
        var text = await DocxTextExtractor.ExtractTextAsync(docx);

        Assert.Contains("Hello world", text);           // adjacent runs, space preserved
        Assert.Contains("Second\nline", text);           // <w:br/> becomes a newline
        Assert.Contains('\n', text);                     // paragraph boundary
    }

    [Fact]
    public async Task Throws_on_non_docx_zip()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var w = new StreamWriter(zip.CreateEntry("readme.txt").Open());
            w.Write("not a docx");
        }
        ms.Position = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => DocxTextExtractor.ExtractTextAsync(ms));
    }
}

using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class DocxExtractorTests
{
    private readonly DocxExtractor _extractor = new();

    [Fact]
    public void Can_handle_docx_extension()
    {
        Assert.True(_extractor.CanHandle("report.docx"));
        Assert.True(_extractor.CanHandle("Report.DOCX"));
        Assert.False(_extractor.CanHandle("report.doc"));
        Assert.False(_extractor.CanHandle("report.pdf"));
    }

    [Fact]
    public async Task Extracts_paragraphs_from_docx()
    {
        using var stream = TestFixtures.Stream(TestFixtures.DocxFile());
        var result = await _extractor.ExtractAsync(stream, "test.docx", ExtractionOptions.Default);

        Assert.Contains("First paragraph of the DOCX file", result.Text);
        Assert.Contains("Second paragraph", result.Text);
        Assert.Contains("Third paragraph", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Preserves_line_breaks()
    {
        using var stream = TestFixtures.Stream(TestFixtures.DocxFile());
        var result = await _extractor.ExtractAsync(stream, "test.docx", ExtractionOptions.Default);

        // The line break should result in "with a line break" appearing
        Assert.Contains("with a line break", result.Text);
    }

    [Fact]
    public async Task Handles_non_docx_zip_gracefully()
    {
        using var stream = TestFixtures.Stream(TestFixtures.NonDocxZip());
        var result = await _extractor.ExtractAsync(stream, "fake.docx", ExtractionOptions.Default);

        // A .docx that is really some other ZIP is the common case of a renamed file, and it has to
        // come back as a reportable failure rather than an exception: one bad file in a folder of
        // 200 must not abort the other 199. Core's extractor throws InvalidOperationException here
        // and DocxExtractor turns that into a CorruptFile warning with no text.
        Assert.Empty(result.Text);
        Assert.Single(result.Warnings);
        Assert.Contains("CorruptFile", result.Warnings[0].Message);
    }

    [Fact]
    public async Task Produces_paragraphs()
    {
        using var stream = TestFixtures.Stream(TestFixtures.DocxFile());
        var result = await _extractor.ExtractAsync(stream, "test.docx", ExtractionOptions.Default);

        Assert.True(result.Paragraphs.Count >= 3);
        Assert.All(result.Paragraphs, p => Assert.NotNull(p.Text));
    }

    [Fact]
    public async Task Rejects_file_over_size_limit()
    {
        var data = new byte[2000];
        using var stream = new MemoryStream(data);
        var options = new ExtractionOptions { MaxSizeBytes = 100 };

        var result = await _extractor.ExtractAsync(stream, "large.docx", options);
        Assert.Contains("[FileTooLarge]", result.Warnings[0].Message);
    }
}

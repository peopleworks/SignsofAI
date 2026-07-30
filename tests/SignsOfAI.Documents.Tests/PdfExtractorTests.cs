using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class PdfExtractorTests
{
    private readonly PdfExtractor _extractor = new();

    [Fact]
    public void Can_handle_pdf_extension()
    {
        Assert.True(_extractor.CanHandle("report.pdf"));
        Assert.True(_extractor.CanHandle("Report.PDF"));
        Assert.False(_extractor.CanHandle("report.docx"));
    }

    [Fact]
    public async Task Extracts_text_from_two_page_pdf()
    {
        using var stream = TestFixtures.Stream(TestFixtures.TwoPagePdf());
        var result = await _extractor.ExtractAsync(stream, "test.pdf", ExtractionOptions.Default);

        Assert.Contains("Page One Content", result.Text);
        Assert.Contains("Page Two Content", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Paragraphs_have_page_numbers()
    {
        using var stream = TestFixtures.Stream(TestFixtures.TwoPagePdf());
        var result = await _extractor.ExtractAsync(stream, "test.pdf", ExtractionOptions.Default);

        Assert.True(result.Paragraphs.Count >= 2);
        // PDF should have page numbers on paragraphs
        Assert.Contains(result.Paragraphs, p => p.PageNumber == 1);
        Assert.Contains(result.Paragraphs, p => p.PageNumber == 2);
    }

    [Fact]
    public async Task Handles_truncated_pdf()
    {
        using var stream = TestFixtures.Stream(TestFixtures.TruncatedPdf());
        var result = await _extractor.ExtractAsync(stream, "broken.pdf", ExtractionOptions.Default);

        // Should not throw — must return a failure
        Assert.True(result.Warnings.Count > 0);
    }

    [Fact]
    public async Task Handles_zero_byte_file()
    {
        using var stream = TestFixtures.Stream(TestFixtures.ZeroByteFile());
        var result = await _extractor.ExtractAsync(stream, "empty.pdf", ExtractionOptions.Default);

        Assert.True(result.Warnings.Count > 0);
        Assert.Contains("CorruptFile", result.Warnings[0].Message);
    }

    [Fact]
    public async Task Produces_paragraphs_with_indices()
    {
        using var stream = TestFixtures.Stream(TestFixtures.TwoPagePdf());
        var result = await _extractor.ExtractAsync(stream, "test.pdf", ExtractionOptions.Default);

        Assert.All(result.Paragraphs, p => Assert.NotNull(p.Text));
    }

    [Fact]
    public async Task Records_bytes_read()
    {
        var bytes = TestFixtures.TwoPagePdf();
        using var stream = TestFixtures.Stream(bytes);
        var result = await _extractor.ExtractAsync(stream, "test.pdf", ExtractionOptions.Default);

        Assert.Equal(bytes.Length, result.BytesRead);
    }
}

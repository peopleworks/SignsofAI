using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class OdtExtractorTests
{
    private readonly OdtExtractor _extractor = new();

    [Fact]
    public void Can_handle_odt_extension()
    {
        Assert.True(_extractor.CanHandle("document.odt"));
        Assert.True(_extractor.CanHandle("Document.ODT"));
        Assert.False(_extractor.CanHandle("document.docx"));
    }

    [Fact]
    public async Task Extracts_paragraphs_from_odt()
    {
        using var stream = TestFixtures.Stream(TestFixtures.OdtFile());
        var result = await _extractor.ExtractAsync(stream, "test.odt", ExtractionOptions.Default);

        Assert.Contains("First paragraph of the ODT", result.Text);
        Assert.Contains("Second paragraph", result.Text);
        Assert.Contains("heading", result.Text);
        Assert.Contains("Final paragraph", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Extracts_spans_inside_paragraphs()
    {
        using var stream = TestFixtures.Stream(TestFixtures.OdtFile());
        var result = await _extractor.ExtractAsync(stream, "test.odt", ExtractionOptions.Default);

        Assert.Contains("span inside", result.Text);
    }

    [Fact]
    public async Task Handles_corrupt_odt_gracefully()
    {
        using var stream = TestFixtures.Stream(TestFixtures.CorruptOdt());
        var result = await _extractor.ExtractAsync(stream, "broken.odt", ExtractionOptions.Default);

        Assert.True(result.Warnings.Count > 0);
        Assert.Contains("CorruptFile", result.Warnings[0].Message);
    }

    [Fact]
    public async Task Handles_non_zip_as_odt()
    {
        // A plain text file renamed to .odt
        var bytes = System.Text.Encoding.UTF8.GetBytes("Not a ZIP");
        using var stream = new MemoryStream(bytes);

        var result = await _extractor.ExtractAsync(stream, "fake.odt", ExtractionOptions.Default);

        // Should return a failure, not throw
        Assert.True(result.Warnings.Count > 0);
    }

    [Fact]
    public async Task Produces_paragraphs_from_odt()
    {
        using var stream = TestFixtures.Stream(TestFixtures.OdtFile());
        var result = await _extractor.ExtractAsync(stream, "test.odt", ExtractionOptions.Default);

        Assert.True(result.Paragraphs.Count >= 3);
        Assert.All(result.Paragraphs, p => Assert.NotNull(p.Text));
    }
}

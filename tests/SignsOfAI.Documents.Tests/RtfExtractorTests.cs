using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class RtfExtractorTests
{
    private readonly RtfExtractor _extractor = new();

    [Fact]
    public void Can_handle_rtf_extension()
    {
        Assert.True(_extractor.CanHandle("document.rtf"));
        Assert.True(_extractor.CanHandle("Document.RTF"));
        Assert.False(_extractor.CanHandle("document.txt"));
    }

    [Fact]
    public async Task Extracts_text_from_rtf()
    {
        using var stream = TestFixtures.Stream(TestFixtures.RtfFile());
        var result = await _extractor.ExtractAsync(stream, "test.rtf", ExtractionOptions.Default);

        Assert.Contains("First paragraph of RTF", result.Text);
        Assert.Contains("Second paragraph", result.Text);
        Assert.Contains("Third paragraph", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Strips_bold_and_italic_control_words()
    {
        using var stream = TestFixtures.Stream(TestFixtures.RtfFile());
        var result = await _extractor.ExtractAsync(stream, "test.rtf", ExtractionOptions.Default);

        Assert.DoesNotContain("\\b", result.Text);
        Assert.DoesNotContain("\\i", result.Text);
        Assert.Contains("bold", result.Text);
        Assert.Contains("italic", result.Text);
    }

    [Fact]
    public async Task Converts_par_to_newlines()
    {
        using var stream = TestFixtures.Stream(TestFixtures.RtfFile());
        var result = await _extractor.ExtractAsync(stream, "test.rtf", ExtractionOptions.Default);

        // Paragraphs should be separated
        Assert.True(result.Paragraphs.Count >= 3);
    }

    [Fact]
    public async Task Strips_font_table()
    {
        using var stream = TestFixtures.Stream(TestFixtures.RtfFile());
        var result = await _extractor.ExtractAsync(stream, "test.rtf", ExtractionOptions.Default);

        Assert.DoesNotContain("fonttbl", result.Text);
        Assert.DoesNotContain("Helvetica", result.Text);
    }

    [Fact]
    public async Task Rejects_non_rtf_content()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("This is not RTF at all.");
        using var stream = new MemoryStream(bytes);

        var result = await _extractor.ExtractAsync(stream, "fake.rtf", ExtractionOptions.Default);

        Assert.Contains("[CorruptFile]", result.Warnings[0].Message);
    }

    [Fact]
    public async Task Produces_paragraphs()
    {
        using var stream = TestFixtures.Stream(TestFixtures.RtfFile());
        var result = await _extractor.ExtractAsync(stream, "test.rtf", ExtractionOptions.Default);

        Assert.True(result.Paragraphs.Count >= 3);
        Assert.All(result.Paragraphs, p => Assert.NotNull(p.Text));
    }

    [Theory]
    [InlineData(@"{\rtf1 Hello \par World}")]
    [InlineData(@"{\rtf1\ansi Line one\line Line two\par}")]
    public async Task StripRtf_handles_basic_cases(string rtf)
    {
        var text = RtfExtractor.StripRtf(rtf);

        Assert.NotEmpty(text);
        Assert.DoesNotContain("\\rtf", text);
        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("}", text);
    }
}

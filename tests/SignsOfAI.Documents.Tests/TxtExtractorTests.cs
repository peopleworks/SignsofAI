using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class TxtExtractorTests
{
    private readonly TxtExtractor _extractor = new();

    [Fact]
    public void Can_handle_txt_and_common_text_extensions()
    {
        Assert.True(_extractor.CanHandle("readme.txt"));
        Assert.True(_extractor.CanHandle("data.csv"));
        Assert.True(_extractor.CanHandle("app.log"));
        Assert.True(_extractor.CanHandle("config.json"));
        Assert.True(_extractor.CanHandle("index.html"));
        Assert.False(_extractor.CanHandle("report.pdf"));
        Assert.False(_extractor.CanHandle("notes.docx"));
    }

    [Fact]
    public async Task Extracts_utf8_text()
    {
        using var stream = TestFixtures.Stream(TestFixtures.Utf8Txt());
        var result = await _extractor.ExtractAsync(stream, "test.txt", ExtractionOptions.Default);

        Assert.Contains("Hello world", result.Text);
        Assert.Contains("paragraph two", result.Text);
        Assert.True(result.Paragraphs.Count >= 2);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Extracts_utf16_le_with_bom()
    {
        using var stream = TestFixtures.Stream(TestFixtures.Utf16LeTxt());
        var result = await _extractor.ExtractAsync(stream, "test.txt", ExtractionOptions.Default);

        Assert.Contains("Hello with BOM", result.Text);
        Assert.Contains("Second paragraph", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Extracts_utf8_with_bom()
    {
        using var stream = TestFixtures.Stream(TestFixtures.Utf8BomTxt());
        var result = await _extractor.ExtractAsync(stream, "test.txt", ExtractionOptions.Default);

        Assert.Contains("Hello with UTF-8 BOM", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Falls_back_to_latin1()
    {
        using var stream = TestFixtures.Stream(TestFixtures.Latin1Txt());
        var result = await _extractor.ExtractAsync(stream, "test.txt", ExtractionOptions.Default);

        // Latin-1 text should decode without replacement characters
        Assert.Contains("Café", result.Text);
        Assert.Contains("résumé", result.Text);
        Assert.DoesNotContain("�", result.Text);
    }

    [Fact]
    public async Task Rejects_file_over_size_limit()
    {
        var data = new byte[1000];
        using var stream = new MemoryStream(data);
        var options = new ExtractionOptions { MaxSizeBytes = 100 };

        var result = await _extractor.ExtractAsync(stream, "large.txt", options);

        Assert.Contains("[FileTooLarge]", result.Warnings[0].Message);
    }

    [Fact]
    public async Task Paragraphs_have_correct_indices()
    {
        using var stream = TestFixtures.Stream(TestFixtures.Utf8Txt());
        var result = await _extractor.ExtractAsync(stream, "test.txt", ExtractionOptions.Default);

        Assert.All(result.Paragraphs, p => Assert.NotNull(p.Text));
        for (int i = 0; i < result.Paragraphs.Count; i++)
        {
            Assert.Equal(i, result.Paragraphs[i].Index);
            Assert.Null(result.Paragraphs[i].PageNumber); // TXT has no page numbers
        }
    }

    [Theory]
    [InlineData("doc.txt", true)]
    [InlineData("notes.md", false)]   // MD has its own extractor
    [InlineData("image.png", false)]
    public void CanHandle_returns_correctly(string fileName, bool expected)
    {
        Assert.Equal(expected, _extractor.CanHandle(fileName));
    }
}

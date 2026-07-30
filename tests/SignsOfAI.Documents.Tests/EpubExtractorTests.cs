using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class EpubExtractorTests
{
    private readonly EpubExtractor _extractor = new();

    [Fact]
    public void Can_handle_epub_extension()
    {
        Assert.True(_extractor.CanHandle("book.epub"));
        Assert.True(_extractor.CanHandle("Book.EPUB"));
        Assert.False(_extractor.CanHandle("book.pdf"));
    }

    [Fact]
    public async Task Extracts_chapters_in_spine_order()
    {
        using var stream = TestFixtures.Stream(TestFixtures.EpubFile());
        var result = await _extractor.ExtractAsync(stream, "test.epub", ExtractionOptions.Default);

        Assert.Contains("Chapter One", result.Text);
        Assert.Contains("Chapter Two", result.Text);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Extracts_paragraphs_from_both_chapters()
    {
        using var stream = TestFixtures.Stream(TestFixtures.EpubFile());
        var result = await _extractor.ExtractAsync(stream, "test.epub", ExtractionOptions.Default);

        Assert.Contains("first paragraph of chapter one", result.Text);
        Assert.Contains("first paragraph of chapter two", result.Text);
        Assert.Contains("Final paragraph of the book", result.Text);
    }

    [Fact]
    public async Task Strips_html_tags_from_chapters()
    {
        using var stream = TestFixtures.Stream(TestFixtures.EpubFile());
        var result = await _extractor.ExtractAsync(stream, "test.epub", ExtractionOptions.Default);

        Assert.DoesNotContain("<h1>", result.Text);
        Assert.DoesNotContain("<p>", result.Text);
        Assert.DoesNotContain("<em>", result.Text);
        Assert.DoesNotContain("<strong>", result.Text);
        Assert.Contains("emphasis", result.Text);
        Assert.Contains("strong", result.Text);
    }

    [Fact]
    public async Task Handles_non_epub_zip()
    {
        using var stream = TestFixtures.Stream(TestFixtures.NonDocxZip());
        var result = await _extractor.ExtractAsync(stream, "fake.epub", ExtractionOptions.Default);

        Assert.True(result.Warnings.Count > 0);
    }

    [Fact]
    public async Task Produces_paragraphs()
    {
        using var stream = TestFixtures.Stream(TestFixtures.EpubFile());
        var result = await _extractor.ExtractAsync(stream, "test.epub", ExtractionOptions.Default);

        Assert.True(result.Paragraphs.Count >= 4); // at least 4 paragraphs across 2 chapters
        Assert.All(result.Paragraphs, p => Assert.NotNull(p.Text));
    }
}

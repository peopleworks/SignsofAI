using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class MarkdownExtractorTests
{
    private readonly MarkdownExtractor _extractor = new();

    [Fact]
    public void Can_handle_markdown_extensions()
    {
        Assert.True(_extractor.CanHandle("readme.md"));
        Assert.True(_extractor.CanHandle("notes.markdown"));
        Assert.True(_extractor.CanHandle("README.MD"));
        Assert.False(_extractor.CanHandle("notes.txt"));
    }

    [Fact]
    public async Task Strips_headers()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("# ", result.Text);
        Assert.DoesNotContain("## ", result.Text);
        Assert.Contains("Heading 1", result.Text);
    }

    [Fact]
    public async Task Strips_bold_and_italic_markers()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("**", result.Text);
        Assert.DoesNotContain("*italic*", result.Text);
        Assert.Contains("bold", result.Text);
        Assert.Contains("italic", result.Text);
    }

    [Fact]
    public async Task Converts_links_to_display_text()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("[link]", result.Text);
        Assert.DoesNotContain("https://example.com", result.Text);
        Assert.Contains("link", result.Text);
    }

    [Fact]
    public async Task Removes_images()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("![alt]", result.Text);
        Assert.DoesNotContain("img.png", result.Text);
    }

    [Fact]
    public async Task Removes_fenced_code_blocks()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("var x = 1", result.Text);
        Assert.DoesNotContain("```", result.Text);
    }

    [Fact]
    public async Task Strips_blockquote_markers()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("> ", result.Text);
        Assert.Contains("blockquote", result.Text);
    }

    [Fact]
    public async Task Strips_list_markers()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("- ", result.Text);
        Assert.DoesNotContain("1. ", result.Text);
        Assert.Contains("List item one", result.Text);
        Assert.Contains("Ordered first", result.Text);
    }

    [Fact]
    public async Task Strips_horizontal_rules()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("---", result.Text);
    }

    [Fact]
    public async Task Strips_strikethrough_markers()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.DoesNotContain("~~", result.Text);
        Assert.Contains("strikethrough", result.Text);
    }

    [Fact]
    public async Task Preserves_prose_paragraphs()
    {
        using var stream = TestFixtures.Stream(TestFixtures.MarkdownFile());
        var result = await _extractor.ExtractAsync(stream, "test.md", ExtractionOptions.Default);

        Assert.Contains("Plain paragraph after code block", result.Text);
        Assert.Contains("Final paragraph", result.Text);
        Assert.True(result.Paragraphs.Count >= 5);
    }
}

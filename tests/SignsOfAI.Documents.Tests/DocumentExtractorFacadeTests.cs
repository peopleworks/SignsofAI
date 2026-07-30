using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents.Tests;

public class DocumentExtractorFacadeTests
{
    private readonly DocumentExtractorFacade _facade = DocumentExtractorFacade.CreateWithDefaults();

    [Fact]
    public void Default_facade_has_all_extractors()
    {
        var types = _facade.Extractors.Select(e => e.GetType()).ToHashSet();
        Assert.Contains(typeof(TxtExtractor), types);
        Assert.Contains(typeof(MarkdownExtractor), types);
        Assert.Contains(typeof(DocxExtractor), types);
        Assert.Contains(typeof(OdtExtractor), types);
        Assert.Contains(typeof(EpubExtractor), types);
        Assert.Contains(typeof(RtfExtractor), types);
        Assert.Contains(typeof(PdfExtractor), types);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".docx")]
    [InlineData(".odt")]
    [InlineData(".epub")]
    [InlineData(".rtf")]
    [InlineData(".pdf")]
    public void Every_supported_format_has_an_extractor(string extension)
    {
        var fileName = $"test{extension}";
        Assert.True(_facade.Extractors.Any(e => e.CanHandle(fileName)),
            $"No extractor registered for '{extension}'");
    }

    [Fact]
    public async Task Unsupported_format_returns_failure()
    {
        var outcome = await _facade.ExtractOneAsync("nonexistent.xyz");

        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.Failure);
        Assert.Equal(ExtractionFailureReason.UnsupportedFormat, outcome.Failure!.Reason);
    }

    [Fact]
    public async Task File_not_found_returns_failure()
    {
        // Must not throw
        var outcome = await _facade.ExtractOneAsync(
            Path.Combine(Path.GetTempPath(), "does_not_exist_12345.txt"));

        // This will be either "file not found" or "unsupported" depending on extension
        Assert.False(outcome.IsSuccess);
    }

    [Fact]
    public async Task Extraction_never_throws_on_any_file()
    {
        // This is the single most important test: a bad file must never kill the batch.
        var tempDir = Path.Combine(Path.GetTempPath(), "SignsOfAI_Docs_Tests");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Write a mix of good and deliberately broken files
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "good.txt"), TestFixtures.Utf8Txt());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "good.md"), TestFixtures.MarkdownFile());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "good.docx"), TestFixtures.DocxFile());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "good.rtf"), TestFixtures.RtfFile());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "broken.pdf"), TestFixtures.TruncatedPdf());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "empty.pdf"), TestFixtures.ZeroByteFile());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "notadocx.docx"), TestFixtures.NonDocxZip());
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "unsupported.xyz"), "garbage"u8.ToArray());

            var outcomes = await _facade.ExtractDirectoryAsync(tempDir);

            Assert.NotEmpty(outcomes);
            // Every outcome should be either success or failure — never null
            Assert.All(outcomes, o => Assert.True(o.Result is not null ^ o.Failure is not null,
                $"Outcome for {o.FilePath} must have either Result or Failure"));

            // The good files should succeed
            var txtOutcome = outcomes.FirstOrDefault(o => o.FilePath.EndsWith("good.txt"));
            Assert.NotNull(txtOutcome);
            Assert.True(txtOutcome!.IsSuccess);

            // The broken files should fail gracefully
            var brokenPdf = outcomes.FirstOrDefault(o => o.FilePath.EndsWith("broken.pdf"));
            Assert.NotNull(brokenPdf);
            Assert.False(brokenPdf!.IsSuccess);

            var emptyOutcome = outcomes.FirstOrDefault(o => o.FilePath.EndsWith("empty.pdf"));
            Assert.NotNull(emptyOutcome);
            Assert.False(emptyOutcome!.IsSuccess);

            // The unsupported file should be skipped (not even attempted)
            Assert.DoesNotContain(outcomes, o => o.FilePath.EndsWith("unsupported.xyz"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Directory_not_found_returns_failure()
    {
        var outcomes = await _facade.ExtractDirectoryAsync(@"C:\this_directory_does_not_exist_12345\");

        Assert.Single(outcomes);
        Assert.False(outcomes[0].IsSuccess);
    }

    [Fact]
    public async Task Successful_extraction_has_result()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllBytesAsync(tempFile, TestFixtures.Utf8Txt());

            var outcome = await _facade.ExtractOneAsync(tempFile);

            Assert.True(outcome.IsSuccess);
            Assert.NotNull(outcome.Result);
            Assert.Contains("Hello world", outcome.Result!.Text);
            Assert.Equal(Path.GetFileName(tempFile), outcome.Result.FileName);
            Assert.True(outcome.Result.BytesRead > 0);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Too_large_file_is_rejected_before_opening()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.txt");
        try
        {
            // Create a 100 KB file
            await File.WriteAllBytesAsync(tempFile, new byte[100_000]);

            var options = new ExtractionOptions { MaxSizeBytes = 1000 };
            var outcome = await _facade.ExtractOneAsync(tempFile, options);

            Assert.False(outcome.IsSuccess);
            Assert.Equal(ExtractionFailureReason.FileTooLarge, outcome.Failure!.Reason);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Facade_picks_correct_extractor_by_extension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.rtf");
        try
        {
            await File.WriteAllBytesAsync(tempFile, TestFixtures.RtfFile());

            var outcome = await _facade.ExtractOneAsync(tempFile);

            Assert.True(outcome.IsSuccess);
            Assert.Contains("First paragraph of RTF", outcome.Result!.Text);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task All_outcomes_report_file_path()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"SignsOfAI_Facade_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "one.txt"), TestFixtures.Utf8Txt());

            var outcomes = await _facade.ExtractDirectoryAsync(tempDir);

            Assert.NotEmpty(outcomes);
            Assert.All(outcomes, o => Assert.StartsWith(tempDir, o.FilePath));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Facade_supports_cancellation()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllBytesAsync(tempFile, TestFixtures.Utf8Txt());

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _facade.ExtractOneAsync(tempFile, ct: cts.Token));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best effort */ }
        }
    }
}

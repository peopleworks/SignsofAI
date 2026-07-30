using SignsOfAI.Core.Documents;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts text from Microsoft Word .docx files by delegating to the zero-dependency
/// <see cref="DocxTextExtractor"/> in Core, then splitting the result into paragraphs.
/// </summary>
public sealed class DocxExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx",
    };

    public bool CanHandle(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName));

    public async Task<ExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        // Buffer to check size before passing to the extractor.
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (options.MaxSizeBytes is { } max && bytes.Length > max)
        {
            return BuildFailure(fileName, ExtractionFailureReason.FileTooLarge,
                $"File is {bytes.Length:N0} bytes; limit is {max:N0} bytes.", bytes.Length);
        }

        ms.Position = 0;

        try
        {
            var text = await DocxTextExtractor.ExtractTextAsync(ms, ct);
            // DOCX paragraphs are separated by single newlines from the extractor;
            // build paragraph spans directly from each non-empty line.
            var paragraphs = BuildParagraphsFromLines(text);

            return new ExtractionResult
            {
                Text = text,
                Paragraphs = paragraphs,
                Warnings = [],
                FileName = fileName,
                BytesRead = bytes.Length,
            };
        }
        catch (InvalidOperationException ex)
        {
            return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                $"Not a valid DOCX: {ex.Message}", bytes.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuildFailure(fileName, ExtractionFailureReason.UnexpectedError,
                $"Failed to extract DOCX: {ex.Message}", bytes.Length);
        }
    }

    private static IReadOnlyList<ParagraphSpan> BuildParagraphsFromLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraphs = new List<ParagraphSpan>();
        var index = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                paragraphs.Add(new ParagraphSpan(index++, trimmed, null));
        }
        return paragraphs;
    }

    private static ExtractionResult BuildFailure(
        string fileName, ExtractionFailureReason reason, string message, long bytesRead)
    {
        return new ExtractionResult
        {
            Text = "",
            Paragraphs = [],
            Warnings = [new ExtractionWarning($"[{reason}] {message}", null)],
            FileName = fileName,
            BytesRead = bytesRead,
        };
    }
}

using System.Text;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts text from PDF files using <c>UglyToad.PdfPig</c> (Apache-2.0, pure managed code).
/// Extracts text page by page and annotates each paragraph with its page number so findings
/// can be mapped back to where the user can see them.
/// </summary>
public sealed class PdfExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    public bool CanHandle(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName));

    public async Task<ExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        // Buffer to check size first and satisfy PdfPig's seekable requirement.
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (options.MaxSizeBytes is { } max && bytes.Length > max)
        {
            return BuildFailure(fileName, ExtractionFailureReason.FileTooLarge,
                $"File is {bytes.Length:N0} bytes; limit is {max:N0} bytes.", bytes.Length);
        }

        // 0-byte file
        if (bytes.Length == 0)
        {
            return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                "The file is empty (0 bytes).", 0);
        }

        ms.Position = 0;

        try
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(
                ms,
                new UglyToad.PdfPig.ParsingOptions
                {
                    // No password — encrypted PDFs will throw
                });

            var maxPages = options.MaxPages ?? int.MaxValue;
            var pageCount = document.NumberOfPages;

            if (pageCount > maxPages)
            {
                return BuildFailure(fileName, ExtractionFailureReason.TooManyPages,
                    $"PDF has {pageCount:N0} pages; limit is {maxPages:N0}.", bytes.Length);
            }

            var fullText = new StringBuilder();
            var paragraphs = new List<ParagraphSpan>();
            var warnings = new List<ExtractionWarning>();
            var paragraphIndex = 0;
            var pagesWithText = 0;

            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                var pageNum = page.Number;
                var pageText = page.Text ?? "";

                if (!string.IsNullOrWhiteSpace(pageText))
                    pagesWithText++;

                // Split page text into paragraphs on blank lines
                var pageParagraphs = pageText
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');

                var sb = new StringBuilder();
                foreach (var line in pageParagraphs)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (sb.Length > 0)
                        {
                            var pText = sb.ToString().Trim();
                            paragraphs.Add(new ParagraphSpan(paragraphIndex++, pText, pageNum));
                            fullText.AppendLine(pText);
                            sb.Clear();
                        }
                    }
                    else
                    {
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(line.Trim());
                    }
                }

                // Flush last paragraph on the page
                if (sb.Length > 0)
                {
                    var pText = sb.ToString().Trim();
                    paragraphs.Add(new ParagraphSpan(paragraphIndex++, pText, pageNum));
                    fullText.AppendLine(pText);
                }
            }

            // If no page had any text, the PDF is likely scanned images only
            if (pagesWithText == 0 && pageCount > 0)
            {
                return BuildFailure(fileName, ExtractionFailureReason.NoTextLayer,
                    $"This PDF appears to be scanned images only — all {pageCount} pages have no accessible text layer.",
                    bytes.Length);
            }

            // Warn about pages with no text (mixed scanned + text pages)
            if (pagesWithText < pageCount)
            {
                warnings.Add(new ExtractionWarning(
                    $"{pageCount - pagesWithText} of {pageCount} pages contain no text " +
                    "(likely scanned images).", null));
            }

            return new ExtractionResult
            {
                Text = fullText.ToString().Trim(),
                Paragraphs = paragraphs,
                Warnings = warnings,
                FileName = fileName,
                BytesRead = bytes.Length,
            };
        }
        catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException)
        {
            return BuildFailure(fileName, ExtractionFailureReason.Encrypted,
                "This PDF is password-protected/encrypted and cannot be read.", bytes.Length);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return BuildFailure(fileName, ExtractionFailureReason.Encrypted,
                $"This PDF is password-protected: {ex.Message}", bytes.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                $"Failed to parse PDF: {ex.Message}", bytes.Length);
        }
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

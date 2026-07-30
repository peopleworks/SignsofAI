using System.Text;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts text from plain-text files (.txt). Handles UTF-8, UTF-16 LE/BE (with BOM),
/// and falls back to Windows-1252 when all else fails — a real-world mix that covers
/// files exported from Notepad, WordPad, and legacy tools.
/// </summary>
public sealed class TxtExtractor : IDocumentExtractor
{
    // Markdown gets its own extractor; plain .txt is just the raw bytes decoded.
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".text", ".log", ".csv", ".json", ".xml", ".html", ".htm", ".css", ".js",
        ".ts", ".cs", ".vb", ".fs", ".py", ".rb", ".go", ".rs", ".java", ".c", ".cpp",
        ".h", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
    };

    public bool CanHandle(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName));

    public async Task<ExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        // Buffer into memory so we can inspect the BOM and re-decode if needed.
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (options.MaxSizeBytes is { } max && bytes.Length > max)
        {
            return BuildFailure(fileName, ExtractionFailureReason.FileTooLarge,
                $"File is {bytes.Length:N0} bytes; limit is {max:N0} bytes.", bytes.Length);
        }

        var text = Decode(bytes, out var encodingUsed);
        var paragraphs = SplitParagraphs(text);

        return new ExtractionResult
        {
            Text = text,
            Paragraphs = paragraphs,
            Warnings = [],
            FileName = fileName,
            BytesRead = bytes.Length,
        };
    }

    /// <summary>
    /// Decode bytes to a string. Tries BOM-prefixed UTF-8/16, then clean UTF-8, then
    /// Windows-1252 as a last-resort fallback that never throws on any byte sequence.
    /// </summary>
    internal static string Decode(byte[] bytes, out string encodingUsed)
    {
        // Check for BOM
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingUsed = "UTF-8 (BOM)";
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encodingUsed = "UTF-16 LE (BOM)";
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encodingUsed = "UTF-16 BE (BOM)";
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // No BOM — try UTF-8 first. If it decodes cleanly, use it.
        try
        {
            var utf8 = Encoding.UTF8.GetString(bytes);
            // If the text contains replacement characters, UTF-8 wasn't right —
            // fall through to Windows-1252 which decodes every byte 1:1.
            if (!utf8.Contains('�'))
            {
                encodingUsed = "UTF-8";
                return utf8;
            }
        }
        catch
        {
            // Fall through
        }

        // Last resort: ISO-8859-1 (Latin-1). Every byte is valid.
        encodingUsed = "ISO-8859-1 (fallback)";
        return Encoding.GetEncoding(28591).GetString(bytes);
    }

    /// <summary>
    /// Splits text into paragraphs on blank-line boundaries. Each paragraph is trimmed;
    /// inline newlines within a paragraph are preserved as spaces so "word-\nwrapped" text
    /// reads naturally.
    /// </summary>
    internal static IReadOnlyList<ParagraphSpan> SplitParagraphs(string text)
    {
        // Normalise CRLF → LF
        var normalised = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // Split on blank-line boundaries (two or more consecutive newlines)
        var blocks = normalised.Split('\n');

        var paragraphs = new List<ParagraphSpan>();
        var sb = new StringBuilder();
        var index = 0;

        foreach (var line in blocks)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (sb.Length > 0)
                {
                    paragraphs.Add(new ParagraphSpan(index++, sb.ToString().Trim(), null));
                    sb.Clear();
                }
            }
            else
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(line.Trim());
            }
        }

        // Flush the last paragraph
        if (sb.Length > 0)
            paragraphs.Add(new ParagraphSpan(index, sb.ToString().Trim(), null));

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

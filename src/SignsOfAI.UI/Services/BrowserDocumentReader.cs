using SignsOfAI.Core.Documents;

namespace SignsOfAI.UI.Services;

/// <summary>
/// The reader the browser build uses: Word and plain text, with no external dependency.
///
/// .docx works because it is a ZIP the BCL can already open — see
/// <see cref="DocxTextExtractor"/>. PDF is deliberately absent: every PDF library is megabytes that
/// every visitor would download before analysing a single word, and the hint says so plainly rather
/// than offering a file type that then fails.
/// </summary>
public sealed class BrowserDocumentReader : IDocumentReader
{
    public string Accept => ".docx,.txt,.md,.markdown,.text,.csv,.log";

    public string HintKey => "home.dochint";

    public async Task<DocumentReadResult> ReadAsync(
        Stream stream, string fileName, long maxBytes, CancellationToken ct = default)
    {
        if (fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return new DocumentReadResult(await DocxTextExtractor.ExtractTextAsync(stream, ct));

        using var reader = new StreamReader(stream);
        return new DocumentReadResult(await reader.ReadToEndAsync(ct));
    }
}

// System.IO is not in a WPF project's implicit usings — that set is shorter than the default one.
using System.IO;
using SignsOfAI.Documents;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop;

/// <summary>
/// The reader the desktop build uses: everything <see cref="SignsOfAI.Documents"/> can read — PDF,
/// ODT, EPUB and RTF on top of the Word and plain text the browser already handles.
///
/// This is the first thing the desktop app does that a browser tab cannot. Not because a browser
/// could not parse a PDF, but because shipping a PDF parser to every visitor costs them megabytes
/// before they analyse a single word. Here it is already on disk.
/// </summary>
public sealed class DesktopDocumentReader : IDocumentReader
{
    private readonly DocumentExtractorFacade _facade = DocumentExtractorFacade.CreateWithDefaults();

    public string Accept => ".pdf,.docx,.odt,.epub,.rtf,.txt,.md,.markdown,.text,.csv,.log";

    public string HintKey => "home.dochint.desktop";

    public async Task<DocumentReadResult> ReadAsync(
        Stream stream, string fileName, long maxBytes, CancellationToken ct = default)
    {
        var extractor = _facade.Extractors.FirstOrDefault(e => e.CanHandle(fileName))
            ?? throw new NotSupportedException($"No reader for {Path.GetExtension(fileName)} files.");

        var options = new ExtractionOptions { MaxSizeBytes = maxBytes };
        var result = await extractor.ExtractAsync(stream, fileName, options, ct);

        // The library reports trouble as warnings rather than exceptions, so that one unreadable file
        // never aborts a batch. A single interactive pick is the one case where silence would be
        // wrong: an empty box with no explanation reads as the app being broken.
        var warning = result.Warnings.Count > 0
            ? string.Join(" ", result.Warnings.Select(w => w.Message))
            : null;

        if (string.IsNullOrWhiteSpace(result.Text) && warning is not null)
            throw new InvalidOperationException(warning);

        return new DocumentReadResult(result.Text, warning);
    }
}

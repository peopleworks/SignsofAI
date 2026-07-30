namespace SignsOfAI.Documents;

/// <summary>
/// Extracts analysable plain text from a document stream. Implementations handle one file
/// format each; the registry/facade picks the right one by file extension.
/// </summary>
public interface IDocumentExtractor
{
    /// <summary>
    /// Returns <c>true</c> when this extractor can handle a file with the given name.
    /// Matching is case-insensitive and should check the extension, not just the final
    /// part — some formats have compound extensions (e.g. <c>.docx</c>).
    /// </summary>
    bool CanHandle(string fileName);

    /// <summary>
    /// Extracts plain text and positional structure from the given stream.
    /// The stream may not be seekable; extractors that need random access must buffer.
    /// Implementations must catch all exceptions and return an <see cref="ExtractionFailure"/>
    /// — this method must never throw, because a single bad file in a batch of 200 must not
    /// kill the loop.
    /// </summary>
    Task<ExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        ExtractionOptions options,
        CancellationToken ct = default);
}

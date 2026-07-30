namespace SignsOfAI.Documents;

/// <summary>
/// The result of a successful extraction from a single file. Even successful extractions may
/// carry <see cref="Warnings"/> — the text is usable, but the caller should tell the user
/// about the caveats.
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// The full extracted plain text, suitable for feeding into the AI-writing analyser.
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>
    /// Paragraphs in document order. Each carries a page number (when the format supports it)
    /// so findings can be mapped back to a visual location the user can check.
    /// </summary>
    public IReadOnlyList<ParagraphSpan> Paragraphs { get; init; } = [];

    /// <summary>
    /// Non-fatal issues encountered during extraction. An empty list means clean extraction.
    /// </summary>
    public IReadOnlyList<ExtractionWarning> Warnings { get; init; } = [];

    /// <summary>
    /// The file name as provided (not the full path), for display purposes.
    /// </summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// How many bytes were read from the stream/file. Useful for progress reporting.
    /// </summary>
    public long BytesRead { get; init; }
}

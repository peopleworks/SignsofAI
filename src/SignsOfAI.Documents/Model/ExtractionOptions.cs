namespace SignsOfAI.Documents;

/// <summary>
/// Guard rails for document extraction. Designed so a 400 MB PDF or a 10 000-page EPUB
/// won't exhaust memory or hang the process.
/// </summary>
public class ExtractionOptions
{
    /// <summary>
    /// Files larger than this are rejected with <see cref="ExtractionFailureReason.FileTooLarge"/>.
    /// Default: 100 MB. Set to <c>null</c> to disable the size check (not recommended).
    /// </summary>
    public long? MaxSizeBytes { get; init; } = 100 * 1024 * 1024;

    /// <summary>
    /// PDFs with more pages than this are rejected with <see cref="ExtractionFailureReason.TooManyPages"/>.
    /// Default: 2 000. Set to <c>null</c> to disable.
    /// </summary>
    public int? MaxPages { get; init; } = 2000;

    /// <summary>
    /// Default options: 100 MB max file size, 2 000 max pages.
    /// </summary>
    public static ExtractionOptions Default { get; } = new();
}

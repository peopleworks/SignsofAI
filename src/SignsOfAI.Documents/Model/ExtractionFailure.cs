namespace SignsOfAI.Documents;

/// <summary>
/// Reason why a file could not be extracted. The caller can group failures by reason
/// in the UI ("12 encrypted PDFs", "3 files exceeded the size limit").
/// </summary>
public enum ExtractionFailureReason
{
    /// <summary>No extractor was registered for this file extension.</summary>
    UnsupportedFormat,

    /// <summary>The file exceeds <see cref="ExtractionOptions.MaxSizeBytes"/>.</summary>
    FileTooLarge,

    /// <summary>The PDF exceeds <see cref="ExtractionOptions.MaxPages"/>.</summary>
    TooManyPages,

    /// <summary>The file is encrypted/password-protected and cannot be read.</summary>
    Encrypted,

    /// <summary>
    /// The file looks like the right format (right extension, right ZIP structure) but its
    /// internal structure is broken or missing required entries.
    /// </summary>
    CorruptFile,

    /// <summary>
    /// The PDF contains only scanned images with no accessible text layer.
    /// </summary>
    NoTextLayer,

    /// <summary>
    /// An unexpected error occurred. The <see cref="ExtractionFailure.Message"/> carries
    /// the exception text.
    /// </summary>
    UnexpectedError,
}

/// <summary>
/// A typed failure for a single file that could not be extracted. Returned instead of
/// throwing, so a batch over 200 files never dies on file 3.
/// </summary>
public class ExtractionFailure
{
    public string FileName { get; init; } = "";
    public ExtractionFailureReason Reason { get; init; }
    public string Message { get; init; } = "";

    public override string ToString() => $"{FileName}: [{Reason}] {Message}";
}

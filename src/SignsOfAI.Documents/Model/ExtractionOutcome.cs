namespace SignsOfAI.Documents;

/// <summary>
/// The outcome of a single extraction attempt — either a successful result or a typed failure.
/// The caller can iterate a batch and separate successes from failures without catching
/// exceptions.
/// </summary>
public class ExtractionOutcome
{
    /// <summary>
    /// The extracted text and structure, or <c>null</c> when extraction failed.
    /// </summary>
    public ExtractionResult? Result { get; init; }

    /// <summary>
    /// Why extraction failed, or <c>null</c> when it succeeded.
    /// </summary>
    public ExtractionFailure? Failure { get; init; }

    /// <summary>
    /// The original file path.
    /// </summary>
    public string FilePath { get; init; } = "";

    public bool IsSuccess => Failure is null;

    public static ExtractionOutcome Ok(string filePath, ExtractionResult result) =>
        new() { FilePath = filePath, Result = result };

    public static ExtractionOutcome Fail(string filePath, ExtractionFailure failure) =>
        new() { FilePath = filePath, Failure = failure };
}

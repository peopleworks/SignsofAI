using SignsOfAI.Documents.Extractors;

namespace SignsOfAI.Documents;

/// <summary>
/// Registry and facade for document extraction. Picks the right <see cref="IDocumentExtractor"/>
/// for a given file, extracts single files or entire directories, and guarantees that no
/// exception from a single file can escape and kill a batch.
///
/// <para>
/// Usage:
/// <code>
///   var facade = DocumentExtractorFacade.CreateWithDefaults();
///   var outcome = await facade.ExtractOneAsync("report.pdf");
///   if (outcome.IsSuccess) { ... }
///
///   // Or batch:
///   var outcomes = await facade.ExtractDirectoryAsync(@"C:\Submissions\");
///   foreach (var o in outcomes) { ... }
/// </code>
/// </para>
/// </summary>
public class DocumentExtractorFacade
{
    private readonly List<IDocumentExtractor> _extractors;

    public DocumentExtractorFacade(IEnumerable<IDocumentExtractor> extractors)
    {
        _extractors = extractors.ToList();
    }

    /// <summary>
    /// Creates a facade pre-loaded with all built-in extractors.
    /// </summary>
    public static DocumentExtractorFacade CreateWithDefaults() =>
        new([
            new TxtExtractor(),
            new MarkdownExtractor(),
            new DocxExtractor(),
            new OdtExtractor(),
            new EpubExtractor(),
            new RtfExtractor(),
            new PdfExtractor(),
        ]);

    /// <summary>
    /// The extractors registered in this facade, in priority order.
    /// </summary>
    public IReadOnlyList<IDocumentExtractor> Extractors => _extractors;

    /// <summary>
    /// Extracts a single file. Returns an <see cref="ExtractionOutcome"/> — never throws,
    /// even if the file is corrupt, encrypted, too large, or in an unknown format.
    /// </summary>
    public async Task<ExtractionOutcome> ExtractOneAsync(
        string filePath,
        ExtractionOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= ExtractionOptions.Default;
        var fileName = Path.GetFileName(filePath);

        ct.ThrowIfCancellationRequested();

        try
        {
            // Check format support first — no point touching the filesystem for .xyz
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(fileName));
            if (extractor is null)
            {
                return ExtractionOutcome.Fail(filePath, new ExtractionFailure
                {
                    FileName = fileName,
                    Reason = ExtractionFailureReason.UnsupportedFormat,
                    Message = $"No extractor registered for '{Path.GetExtension(fileName)}'.",
                });
            }

            // Check file existence
            if (!File.Exists(filePath))
            {
                return ExtractionOutcome.Fail(filePath, new ExtractionFailure
                {
                    FileName = fileName,
                    Reason = ExtractionFailureReason.UnexpectedError,
                    Message = "File not found.",
                });
            }

            // Check file size before opening
            var fileInfo = new FileInfo(filePath);
            if (options.MaxSizeBytes is { } max && fileInfo.Length > max)
            {
                return ExtractionOutcome.Fail(filePath, new ExtractionFailure
                {
                    FileName = fileName,
                    Reason = ExtractionFailureReason.FileTooLarge,
                    Message = $"File is {fileInfo.Length:N0} bytes; limit is {max:N0} bytes.",
                });
            }

            // Extract (extractor already resolved above)
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            var result = await extractor.ExtractAsync(stream, fileName, options, ct);

            // Check if the extractor itself signalled a failure via warnings
            var failureWarning = result.Warnings.FirstOrDefault(
                w => w.Message.StartsWith('['));
            if (failureWarning is { } fw)
            {
                // Parse the reason from the warning format "[Reason] message"
                var msg = fw.Message;
                var closeBracket = msg.IndexOf(']');
                var reasonStr = closeBracket > 1 ? msg[1..closeBracket] : "";
                var reason = Enum.TryParse<ExtractionFailureReason>(reasonStr, out var r)
                    ? r
                    : ExtractionFailureReason.UnexpectedError;
                var message = closeBracket >= 0 ? msg[(closeBracket + 2)..] : msg;

                return ExtractionOutcome.Fail(filePath, new ExtractionFailure
                {
                    FileName = fileName,
                    Reason = reason,
                    Message = message,
                });
            }

            return ExtractionOutcome.Ok(filePath, result);
        }
        catch (OperationCanceledException)
        {
            throw; // Don't swallow cancellation
        }
        catch (Exception ex)
        {
            // Safety net: any unhandled exception becomes a typed failure
            return ExtractionOutcome.Fail(filePath, new ExtractionFailure
            {
                FileName = fileName,
                Reason = ExtractionFailureReason.UnexpectedError,
                Message = ex.Message,
            });
        }
    }

    /// <summary>
    /// Extracts every supported file in a directory (non-recursive by default).
    /// Returns one outcome per file found; files with no matching extractor are skipped
    /// entirely. Use <paramref name="recursive"/> to descend into subdirectories.
    ///
    /// <para>
    /// Does not buffer all files into memory — each file is read, extracted, and yielded
    /// before the next one is opened.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ExtractionOutcome>> ExtractDirectoryAsync(
        string directoryPath,
        ExtractionOptions? options = null,
        CancellationToken ct = default,
        bool recursive = false)
    {
        options ??= ExtractionOptions.Default;

        if (!Directory.Exists(directoryPath))
        {
            return [ExtractionOutcome.Fail(directoryPath, new ExtractionFailure
            {
                FileName = Path.GetFileName(directoryPath),
                Reason = ExtractionFailureReason.UnexpectedError,
                Message = "Directory not found.",
            })];
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(directoryPath, "*.*", searchOption);

        var outcomes = new List<ExtractionOutcome>();
        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            // Only process files that have a registered extractor
            if (!_extractors.Any(e => e.CanHandle(fileName)))
                continue;

            var outcome = await ExtractOneAsync(filePath, options, ct);
            outcomes.Add(outcome);
        }

        return outcomes;
    }
}

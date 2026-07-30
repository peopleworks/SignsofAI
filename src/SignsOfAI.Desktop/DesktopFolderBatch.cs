using System.IO;
using Microsoft.Win32;
using SignsOfAI.Documents;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop;

/// <summary>
/// Folder scanning on the desktop: a real folder dialog, then the documents library one file at a
/// time.
///
/// The library has an ExtractDirectoryAsync that does the whole folder in one call, and this
/// deliberately does not use it — it returns only when every file is done, which for a folder of two
/// hundred submissions means a frozen window, no progress and nothing to cancel. Enumerating here
/// and calling the per-file API keeps that behaviour visible to the user, and the per-file API is
/// the one that already guarantees a bad document cannot end the scan.
/// </summary>
public sealed class DesktopFolderBatch : IFolderBatch
{
    private readonly DocumentExtractorFacade _facade = DocumentExtractorFacade.CreateWithDefaults();

    public bool IsAvailable => true;

    public Task<string?> PickFolderAsync(CancellationToken ct = default)
    {
        // Runs on the WPF dispatcher thread: BlazorWebView already puts component code there, so the
        // dialog can be shown directly without marshalling.
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a folder of documents",
            Multiselect = false,
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FolderName : null);
    }

    public Task<IReadOnlyList<BatchFile>> ListAsync(
        string folder, bool recursive, CancellationToken ct = default)
    {
        if (!Directory.Exists(folder))
            return Task.FromResult<IReadOnlyList<BatchFile>>([]);

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Only files something can actually read. Listing a folder's PNGs and zips as "0 words"
        // would bury the documents the user came for.
        var files = Directory.EnumerateFiles(folder, "*.*", option)
            .Where(p => _facade.Extractors.Any(e => e.CanHandle(Path.GetFileName(p))))
            .Select(p => new FileInfo(p))
            .OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(f => new BatchFile(f.FullName, f.Name, f.Length))
            .ToArray();

        return Task.FromResult<IReadOnlyList<BatchFile>>(files);
    }

    public async Task<BatchRead> ReadAsync(BatchFile file, CancellationToken ct = default)
    {
        var outcome = await _facade.ExtractOneAsync(file.Path, ExtractionOptions.Default, ct);

        if (!outcome.IsSuccess)
            return new BatchRead(file, null, outcome.Failure!.Message);

        var text = outcome.Result!.Text;

        // A PDF of scanned pages extracts cleanly to nothing. Reporting that as an empty document
        // with a score would be a lie; say the page has no text layer.
        if (string.IsNullOrWhiteSpace(text))
        {
            var why = outcome.Result.Warnings.Count > 0
                ? string.Join(" ", outcome.Result.Warnings.Select(w => w.Message))
                : "No text could be extracted — the file may be scanned images.";
            return new BatchRead(file, null, why);
        }

        return new BatchRead(file, text, null);
    }
}

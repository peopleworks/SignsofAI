namespace SignsOfAI.UI.Services;

/// <summary>One readable file found in a folder.</summary>
public sealed record BatchFile(string Path, string Name, long Bytes);

/// <summary>What came of reading one of them. Exactly one of Text/Error is set.</summary>
public sealed record BatchRead(BatchFile File, string? Text, string? Error);

/// <summary>
/// Scanning a whole folder of documents — a teacher with a term's submissions, a writer with a book
/// in chapters.
///
/// Deliberately two phases: list the folder, then read one file at a time. A single call returning
/// everything at the end would leave the interface frozen and silent through two hundred files, and
/// give nothing to cancel. Listing first means the table appears immediately and fills in as the
/// scan proceeds.
///
/// A host that cannot do this — the browser, which is handed files and never a folder path — leaves
/// <see cref="IsAvailable"/> false and the interface simply does not offer it.
/// </summary>
public interface IFolderBatch
{
    /// <summary>False in hosts with no access to a folder. The interface hides the feature.</summary>
    bool IsAvailable { get; }

    /// <summary>The folder the user chose, or null if they cancelled the dialog.</summary>
    Task<string?> PickFolderAsync(CancellationToken ct = default);

    /// <summary>Files in the folder this host can actually read, in name order.</summary>
    Task<IReadOnlyList<BatchFile>> ListAsync(string folder, bool recursive, CancellationToken ct = default);

    /// <summary>
    /// Reads one file. Never throws for a bad document: an unreadable file among two hundred is a
    /// row that says why, not the end of the scan.
    /// </summary>
    Task<BatchRead> ReadAsync(BatchFile file, CancellationToken ct = default);
}

/// <summary>The browser's answer: it is given files, never a folder, so there is nothing to offer.</summary>
public sealed class NoFolderBatch : IFolderBatch
{
    public bool IsAvailable => false;

    public Task<string?> PickFolderAsync(CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    public Task<IReadOnlyList<BatchFile>> ListAsync(string folder, bool recursive, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BatchFile>>([]);

    public Task<BatchRead> ReadAsync(BatchFile file, CancellationToken ct = default) =>
        Task.FromResult(new BatchRead(file, null, "Folder scanning is not available in this host."));
}

namespace SignsOfAI.UI.Services;

/// <summary>How the one-time model download is going.</summary>
public sealed record ModelDownloadState(
    string FileName, long BytesRead, long? TotalBytes, int FileIndex, int FileCount)
{
    /// <summary>Null when the server sent no size — show the megabytes instead of a bar.</summary>
    public int? Percent => TotalBytes is > 0 ? (int)(BytesRead * 100 / TotalBytes.Value) : null;

    public double ReadMb => BytesRead / 1024d / 1024d;
    public double? TotalMb => TotalBytes is > 0 ? TotalBytes.Value / 1024d / 1024d : null;
}

/// <summary>
/// Measuring predictability with a model running inside this application, rather than by calling a
/// server.
///
/// This is the strongest thing the desktop build has: the same reading the hosted endpoint gives,
/// with the text never leaving the machine and no service to be up. The weights are not in the
/// repository — they are hundreds of megabytes — so the first use fetches them once and every use
/// after that works offline.
///
/// The browser cannot do this: an ONNX runtime and a half-gigabyte model are not something to hand
/// a visitor who came to check a paragraph. There, <see cref="IsAvailable"/> is false and the panel
/// keeps offering the optional server.
/// </summary>
public interface ILocalPerplexity
{
    /// <summary>False in hosts with no in-process engine. The panel falls back to the server.</summary>
    bool IsAvailable { get; }

    /// <summary>True once the weights are on disk. False means the first use has to download them.</summary>
    bool FilesReady { get; }

    /// <summary>Short model name for the interface, e.g. "Qwen 0.5B".</summary>
    string ModelLabel { get; }

    /// <summary>Downloads the weights if they are missing. Safe to call when they are already there.</summary>
    Task EnsureFilesAsync(IProgress<ModelDownloadState>? progress, CancellationToken ct = default);

    /// <summary>Scores <paramref name="text"/>, calibrated for <paramref name="language"/>.</summary>
    Task<PerplexityResult> MeasureAsync(string text, string language, CancellationToken ct = default);
}

/// <summary>The browser's answer: no engine here.</summary>
public sealed class NoLocalPerplexity : ILocalPerplexity
{
    public bool IsAvailable => false;
    public bool FilesReady => false;
    public string ModelLabel => "";

    public Task EnsureFilesAsync(IProgress<ModelDownloadState>? progress, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<PerplexityResult> MeasureAsync(string text, string language, CancellationToken ct = default) =>
        throw new NotSupportedException("No in-process model in this host.");
}

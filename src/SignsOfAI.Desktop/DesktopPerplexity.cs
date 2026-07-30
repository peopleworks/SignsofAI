using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using SignsOfAI.Onnx.Config;
using SignsOfAI.Onnx.Engine;
using SignsOfAI.Onnx.Scoring;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop;

/// <summary>
/// Predictability measured by a model running inside this window.
///
/// The engine is the same one the hosted API uses — SignsOfAI.Onnx was extracted from it precisely
/// so both could share it — and the calibration is the same <see cref="PerplexityScorer"/>. That
/// matters: the desktop must not quietly report a different number for the same paragraph than the
/// website does.
///
/// Weights live under %LOCALAPPDATA%, not next to the executable: Program Files is not writable by
/// the user who runs the app, and the download would fail there for exactly the people least likely
/// to work out why.
/// </summary>
public sealed class DesktopPerplexity : ILocalPerplexity, IDisposable
{
    private readonly OnnxModelEngine _engine;
    private readonly ModelProfile _profile;

    public DesktopPerplexity()
    {
        var options = PerplexityOptions.Defaults();
        _profile = options.Models.First(m => m.Id == options.DefaultModel);

        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SignsOfAI");

        _engine = new OnnxModelEngine(_profile, basePath, NullLogger.Instance);
    }

    public bool IsAvailable => true;

    public bool FilesReady => _engine.IsAvailable;

    public string ModelLabel => _profile.Label;

    public Task EnsureFilesAsync(IProgress<ModelDownloadState>? progress, CancellationToken ct = default)
    {
        // The engine reports its own progress shape; the interface layer has its own so that it
        // never has to reference the ONNX assembly. Translating here is the whole cost of that.
        IProgress<ModelDownloadProgress>? bridge = progress is null
            ? null
            : new Progress<ModelDownloadProgress>(p => progress.Report(
                new ModelDownloadState(p.FileName, p.BytesRead, p.TotalBytes, p.FileIndex, p.FileCount)));

        return _engine.EnsureFilesAsync(bridge, ct);
    }

    public async Task<PerplexityResult> MeasureAsync(
        string text, string language, CancellationToken ct = default)
    {
        var raw = await _engine.ScoreAsync(text, ct);
        var score = PerplexityScorer.Score(raw, language, _profile);

        return new PerplexityResult
        {
            Ppl = score.Ppl,
            AvgLogProb = score.AvgLogProb,
            TokenCount = score.TokenCount,
            Predictability = score.Predictability,
            Band = score.Band,
            Model = score.Model,
            Lang = score.Lang,
            ElapsedMs = score.ElapsedMs,
        };
    }

    public void Dispose() => _engine.Dispose();
}

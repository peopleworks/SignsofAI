using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using SignsOfAI.Perplexity.Api.Config;
using Tokenizers.DotNet;

namespace SignsOfAI.Perplexity.Api.Engine;

/// <summary>
/// Computes causal-LM perplexity with a Qwen2.5-0.5B ONNX model via ONNX Runtime (CPU) and the
/// Hugging Face tokenizer. To keep the server light when idle, the model is <b>lazily loaded</b> on
/// the first request and <b>unloaded from RAM after an idle period</b> (see <see cref="TryUnloadIfIdle"/>),
/// reloading from disk (~1-2s) on the next request. Loads/unloads are serialized by a gate; inference
/// runs outside the gate and an active-request counter prevents unloading mid-inference.
/// <see cref="InferenceSession.Run(RunOptions, IReadOnlyDictionary{string, OrtValue}, IReadOnlyCollection{string})"/>
/// is thread-safe, and each call builds its own inputs, so concurrent scoring is safe.
/// </summary>
public sealed class OnnxPerplexityEngine : IPerplexityEngine, IDisposable
{
    private readonly PerplexityOptions _o;
    private readonly ILogger<OnnxPerplexityEngine> _log;
    private readonly string _dir, _modelPath, _tokPath;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private Tokenizer? _tokenizer;
    private int _active;
    private DateTime _lastUsedUtc = DateTime.UtcNow;
    private volatile bool _filesReady;
    private volatile bool _loaded;

    public OnnxPerplexityEngine(PerplexityOptions options, IHostEnvironment env, ILogger<OnnxPerplexityEngine> log)
    {
        _o = options; _log = log;
        _dir = Path.IsPathRooted(_o.ModelDir) ? _o.ModelDir : Path.Combine(env.ContentRootPath, _o.ModelDir);
        _modelPath = Path.Combine(_dir, _o.ModelFile);
        _tokPath = Path.Combine(_dir, _o.TokenizerFile);
    }

    public string ModelId => _o.ModelId;
    public bool IsReady => _filesReady;
    public bool IsLoaded => _loaded;

    /// <summary>Ensures the model + tokenizer FILES exist (downloading once if needed), without loading
    /// them into RAM. Call at startup. If <see cref="PerplexityOptions.PreloadModel"/> is set, also warms.</summary>
    public async Task EnsureFilesAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_dir);
        await EnsureFileAsync(_modelPath, _o.ModelUrl, ct);
        await EnsureFileAsync(_tokPath, _o.TokenizerUrl, ct);

        _filesReady = File.Exists(_modelPath) && File.Exists(_tokPath);
        if (!_filesReady)
        {
            _log.LogError("Perplexity model/tokenizer missing at {Dir} and no download URL configured; engine not ready.", _dir);
            return;
        }
        _log.LogInformation("Perplexity engine ready (files present): {Model}. Idle-unload={Idle}s, preload={Preload}.",
            _o.ModelFile, _o.IdleUnloadSeconds, _o.PreloadModel);

        if (_o.PreloadModel) { var _ = await AcquireAsync(ct); Release(); }
    }

    public async Task<PerplexityRaw> ScoreAsync(string text, CancellationToken ct = default)
    {
        if (!_filesReady) throw new InvalidOperationException("Perplexity engine is not ready.");

        var (session, tokenizer) = await AcquireAsync(ct);
        try
        {
            return await Task.Run(() => Forward(session, tokenizer, text, ct), ct);
        }
        finally { Release(); }
    }

    /// <summary>Loads the model (if needed) and refreshes the idle clock, without running inference.</summary>
    public async Task WarmupAsync(CancellationToken ct = default)
    {
        if (!_filesReady) return;
        _ = await AcquireAsync(ct);
        Release();
    }

    // ── Load / unload lifecycle ──────────────────────────────────────────────
    private async Task<(InferenceSession, Tokenizer)> AcquireAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_session is null) LoadLocked();
            _active++;
            return (_session!, _tokenizer!);
        }
        finally { _gate.Release(); }
    }

    private void Release()
    {
        _gate.Wait();
        try { if (_active > 0) _active--; _lastUsedUtc = DateTime.UtcNow; }
        finally { _gate.Release(); }
    }

    private void LoadLocked()
    {
        var so = new Microsoft.ML.OnnxRuntime.SessionOptions();
        if (_o.IntraOpThreads > 0) so.IntraOpNumThreads = _o.IntraOpThreads;
        var sw = Stopwatch.StartNew();
        _tokenizer = new Tokenizer(_tokPath);
        _session = new InferenceSession(_modelPath, so);
        _loaded = true;
        _log.LogInformation("Perplexity model loaded into RAM in {Ms} ms.", sw.ElapsedMilliseconds);
    }

    /// <summary>If the model is loaded, no request is in flight, and it's been idle longer than
    /// <paramref name="idle"/>, dispose it to free RAM. Non-blocking; returns whether it unloaded.</summary>
    public bool TryUnloadIfIdle(TimeSpan idle)
    {
        if (!_gate.Wait(0)) return false;
        try
        {
            if (_session is null || _active != 0 || DateTime.UtcNow - _lastUsedUtc <= idle)
                return false;
            _session.Dispose(); _session = null;
            _tokenizer?.Dispose(); _tokenizer = null;
            _loaded = false;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            return true;
        }
        finally { _gate.Release(); }
    }

    // ── Inference ─────────────────────────────────────────────────────────────
    private PerplexityRaw Forward(InferenceSession session, Tokenizer tokenizer, string text, CancellationToken ct)
    {
        var ids = Array.ConvertAll(tokenizer.Encode(text), x => (long)x);
        if (ids.Length > _o.MaxTokens) ids = ids[.._o.MaxTokens]; // right-truncate for bounded latency
        if (ids.Length < 2) return new PerplexityRaw(1.0, 0.0, 0, 0);

        int n = ids.Length, vocab = _o.Vocab;
        var toDispose = new List<OrtValue>(2 * _o.NumLayers + 3);
        OrtValue Mk<T>(T[] data, long[] shape) where T : unmanaged
        { var v = OrtValue.CreateTensorValueFromMemory(data, shape); toDispose.Add(v); return v; }

        var sw = Stopwatch.StartNew();
        try
        {
            var inputs = new Dictionary<string, OrtValue>(2 * _o.NumLayers + 3) { ["input_ids"] = Mk(ids, [1, n]) };
            var attn = new long[n]; Array.Fill(attn, 1L);
            inputs["attention_mask"] = Mk(attn, [1, n]);
            var pos = new long[n]; for (int i = 0; i < n; i++) pos[i] = i;
            inputs["position_ids"] = Mk(pos, [1, n]);

            long[] kvShape = [1, _o.NumKvHeads, 0, _o.HeadDim];
            for (int i = 0; i < _o.NumLayers; i++)
            {
                inputs[$"past_key_values.{i}.key"] = Mk(Array.Empty<float>(), kvShape);
                inputs[$"past_key_values.{i}.value"] = Mk(Array.Empty<float>(), kvShape);
            }

            using var ro = new RunOptions();
            using var results = session.Run(ro, inputs, ["logits"]);
            var logits = results[0].GetTensorDataAsSpan<float>(); // [1, n, vocab] row-major

            double sumNll = 0; int scored = 0;
            for (int t = 0; t < n - 1; t++)
            {
                ct.ThrowIfCancellationRequested();
                int off = t * vocab;
                float max = float.NegativeInfinity;
                for (int v = 0; v < vocab; v++) { float z = logits[off + v]; if (z > max) max = z; }
                double sumExp = 0;
                for (int v = 0; v < vocab; v++) sumExp += Math.Exp(logits[off + v] - max);
                double logZ = max + Math.Log(sumExp);
                sumNll += -(logits[off + (int)ids[t + 1]] - logZ);
                scored++;
            }
            sw.Stop();
            double meanNll = sumNll / scored;
            return new PerplexityRaw(Math.Exp(meanNll), -meanNll, scored, sw.ElapsedMilliseconds);
        }
        finally { foreach (var d in toDispose) d.Dispose(); }
    }

    private async Task EnsureFileAsync(string path, string? url, CancellationToken ct)
    {
        if (File.Exists(path) || string.IsNullOrWhiteSpace(url)) return;
        _log.LogInformation("Downloading {Url} → {Path} …", url, path);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var tmp = path + ".part";
        await using (var fs = File.Create(tmp))
            await resp.Content.CopyToAsync(fs, ct);
        File.Move(tmp, path, overwrite: true);
        _log.LogInformation("Downloaded {Path} ({Bytes:N0} bytes).", path, new FileInfo(path).Length);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _tokenizer?.Dispose();
        _gate.Dispose();
    }
}

/// <summary>At startup: ensures the model file is on disk (download once). Then, if idle-unloading is
/// enabled, periodically frees the model from RAM after it goes idle.</summary>
public sealed class ModelLifecycleService(OnnxPerplexityEngine engine, PerplexityOptions options, ILogger<ModelLifecycleService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await engine.EnsureFilesAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Perplexity engine failed to initialize."); }

        if (options.IdleUnloadSeconds <= 0) return; // keep resident once loaded

        var idle = TimeSpan.FromSeconds(options.IdleUnloadSeconds);
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.IdleUnloadSeconds / 4.0, 15, 120));
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
            if (engine.TryUnloadIfIdle(idle))
                log.LogInformation("Perplexity model unloaded after {Idle}s idle — RAM freed.", options.IdleUnloadSeconds);
        }
    }
}

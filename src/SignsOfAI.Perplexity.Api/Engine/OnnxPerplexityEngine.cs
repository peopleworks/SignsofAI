using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using SignsOfAI.Perplexity.Api.Config;
using Tokenizers.DotNet;

namespace SignsOfAI.Perplexity.Api.Engine;

/// <summary>
/// One selectable model. Computes causal-LM perplexity via ONNX Runtime (CPU) + the HF tokenizer for its
/// <see cref="ModelProfile"/>. To keep the server light, the model is <b>lazily loaded</b> on first use and
/// <b>unloaded from RAM after an idle period</b>, reloading from disk on demand. Loads/unloads are serialized
/// by a gate; inference runs outside the gate and an active-request counter prevents unloading mid-inference.
/// </summary>
public sealed class OnnxModelEngine(ModelProfile profile, IHostEnvironment env, ILogger log) : IDisposable
{
    private readonly string _dir = Path.IsPathRooted(profile.ModelDir) ? profile.ModelDir : Path.Combine(env.ContentRootPath, profile.ModelDir);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _downloadLock = new();
    private Task? _downloadTask;
    private InferenceSession? _session;
    private Tokenizer? _tokenizer;
    private int _active;
    private DateTime _lastUsedUtc = DateTime.UtcNow;
    private volatile bool _filesReady;
    private volatile bool _loaded;

    public ModelProfile Profile => profile;
    public string ModelId => profile.Id;
    public bool IsLoaded => _loaded;
    public bool FilesReady => _filesReady;

    /// <summary>Ensures the model files are on disk, awaiting the (single, detached) download. Used at
    /// startup for the default model. The download itself runs on an app-lifetime token, so a request that
    /// triggers it and then disconnects does NOT abort a multi-minute download.</summary>
    public Task EnsureFilesAsync(CancellationToken ct = default) => GetOrStartDownload();

    private Task GetOrStartDownload()
    {
        if (_filesReady) return Task.CompletedTask;
        lock (_downloadLock)
        {
            if (_filesReady) return Task.CompletedTask;
            return _downloadTask ??= Task.Run(DownloadAllAsync);
        }
    }

    private async Task DownloadAllAsync()
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var modelPath = Path.Combine(_dir, profile.ModelFile);
            var tokPath = Path.Combine(_dir, profile.TokenizerFile);
            // CancellationToken.None on purpose: a disconnecting client must not abort the download.
            await EnsureFileAsync(modelPath, profile.ModelUrl, CancellationToken.None);
            await EnsureFileAsync(tokPath, profile.TokenizerUrl, CancellationToken.None);
            foreach (var url in profile.AuxFileUrls)
                await EnsureFileAsync(Path.Combine(_dir, Path.GetFileName(new Uri(url).AbsolutePath)), url, CancellationToken.None);

            _filesReady = File.Exists(modelPath) && File.Exists(tokPath);
            if (!_filesReady) log.LogError("[{Model}] model/tokenizer missing at {Dir} and no download URL configured.", profile.Id, _dir);
            else log.LogInformation("[{Model}] files ready.", profile.Id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "[{Model}] download failed; will retry on next request.", profile.Id);
            lock (_downloadLock) _downloadTask = null; // allow a retry
        }
    }

    public async Task<PerplexityRaw> ScoreAsync(string text, CancellationToken ct = default)
    {
        var (session, tokenizer) = await AcquireAsync(ct);
        try { return await Task.Run(() => Forward(session, tokenizer, text, ct), ct); }
        finally { Release(); }
    }

    /// <summary>Loads the model (if needed) and refreshes the idle clock, without running inference.</summary>
    public async Task WarmupAsync(CancellationToken ct = default)
    {
        _ = await AcquireAsync(ct);
        Release();
    }

    private async Task<(InferenceSession, Tokenizer)> AcquireAsync(CancellationToken ct)
    {
        if (!_filesReady)
        {
            _ = GetOrStartDownload(); // kick off (or continue) the detached download; don't block the request on it
            throw new InvalidOperationException($"Model '{profile.Id}' is downloading on the server; retry shortly.");
        }

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
        if (profile.IntraOpThreads > 0) so.IntraOpNumThreads = profile.IntraOpThreads;
        var sw = Stopwatch.StartNew();
        _tokenizer = new Tokenizer(Path.Combine(_dir, profile.TokenizerFile));
        _session = new InferenceSession(Path.Combine(_dir, profile.ModelFile), so);
        _loaded = true;
        log.LogInformation("[{Model}] loaded into RAM in {Ms} ms.", profile.Id, sw.ElapsedMilliseconds);
    }

    /// <summary>Frees this model from RAM if it's loaded, idle, and no request is in flight.</summary>
    public bool TryUnloadIfIdle(TimeSpan idle)
    {
        if (!_gate.Wait(0)) return false;
        try
        {
            if (_session is null || _active != 0 || DateTime.UtcNow - _lastUsedUtc <= idle) return false;
            _session.Dispose(); _session = null;
            _tokenizer?.Dispose(); _tokenizer = null;
            _loaded = false;
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            return true;
        }
        finally { _gate.Release(); }
    }

    private PerplexityRaw Forward(InferenceSession session, Tokenizer tokenizer, string text, CancellationToken ct)
    {
        var ids = Array.ConvertAll(tokenizer.Encode(text), x => (long)x);
        if (ids.Length > profile.MaxTokens) ids = ids[..profile.MaxTokens];
        if (ids.Length < 2) return new PerplexityRaw(1.0, 0.0, 0, 0);

        int n = ids.Length, vocab = profile.Vocab;
        var toDispose = new List<OrtValue>(2 * profile.NumLayers + 3);
        OrtValue Mk<T>(T[] data, long[] shape) where T : unmanaged
        { var v = OrtValue.CreateTensorValueFromMemory(data, shape); toDispose.Add(v); return v; }

        var sw = Stopwatch.StartNew();
        try
        {
            var inputs = new Dictionary<string, OrtValue>(2 * profile.NumLayers + 3) { ["input_ids"] = Mk(ids, [1, n]) };
            var attn = new long[n]; Array.Fill(attn, 1L);
            inputs["attention_mask"] = Mk(attn, [1, n]);
            // Some exports (Qwen) take position_ids; others (Phi-4) don't — add it only if the graph wants it.
            if (session.InputMetadata.ContainsKey("position_ids"))
            {
                var pos = new long[n]; for (int i = 0; i < n; i++) pos[i] = i;
                inputs["position_ids"] = Mk(pos, [1, n]);
            }

            long[] kvShape = [1, profile.NumKvHeads, 0, profile.HeadDim];
            for (int i = 0; i < profile.NumLayers; i++)
            {
                inputs[$"past_key_values.{i}.key"] = Mk(Array.Empty<float>(), kvShape);
                inputs[$"past_key_values.{i}.value"] = Mk(Array.Empty<float>(), kvShape);
            }

            using var ro = new RunOptions();
            using var results = session.Run(ro, inputs, ["logits"]);
            var logits = results[0].GetTensorDataAsSpan<float>();

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
        log.LogInformation("[{Model}] downloading {Url} …", profile.Id, url);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var tmp = path + ".part";
        await using (var fs = File.Create(tmp)) await resp.Content.CopyToAsync(fs, ct);
        File.Move(tmp, path, overwrite: true);
        log.LogInformation("[{Model}] downloaded {Path} ({Bytes:N0} bytes).", profile.Id, Path.GetFileName(path), new FileInfo(path).Length);
    }

    public void Dispose() { _session?.Dispose(); _tokenizer?.Dispose(); _gate.Dispose(); }
}

/// <summary>Holds one <see cref="OnnxModelEngine"/> per configured model and resolves a request to one.</summary>
public sealed class PerplexityRegistry
{
    private readonly Dictionary<string, OnnxModelEngine> _byId = new(StringComparer.OrdinalIgnoreCase);
    public OnnxModelEngine Default { get; }
    public IReadOnlyCollection<OnnxModelEngine> Engines => _byId.Values;

    public PerplexityRegistry(PerplexityOptions options, IHostEnvironment env, ILoggerFactory lf)
    {
        foreach (var p in options.Models)
            _byId[p.Id] = new OnnxModelEngine(p, env, lf.CreateLogger($"Perplexity.{p.Id}"));
        var defId = options.DefaultModel ?? options.Models.FirstOrDefault()?.Id;
        Default = (defId is not null && _byId.TryGetValue(defId, out var d)) ? d : _byId.Values.First();
    }

    /// <summary>Resolves the requested model id, falling back to the default when null/blank/unknown.</summary>
    public OnnxModelEngine Resolve(string? modelId) =>
        !string.IsNullOrWhiteSpace(modelId) && _byId.TryGetValue(modelId, out var e) ? e : Default;
}

/// <summary>At startup: ensures the DEFAULT model's file is on disk (others download lazily on first use).
/// Then, if idle-unloading is enabled, periodically frees idle models from RAM.</summary>
public sealed class ModelLifecycleService(PerplexityRegistry registry, PerplexityOptions options, ILogger<ModelLifecycleService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await registry.Default.EnsureFilesAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Default model failed to initialize."); }

        if (options.PreloadModel)
            try { await registry.Default.WarmupAsync(ct); } catch { /* best effort */ }

        if (options.IdleUnloadSeconds <= 0) return;
        var idle = TimeSpan.FromSeconds(options.IdleUnloadSeconds);
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.IdleUnloadSeconds / 4.0, 15, 120));
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
            foreach (var e in registry.Engines)
                if (e.TryUnloadIfIdle(idle))
                    log.LogInformation("[{Model}] unloaded after {Idle}s idle — RAM freed.", e.ModelId, options.IdleUnloadSeconds);
        }
    }
}

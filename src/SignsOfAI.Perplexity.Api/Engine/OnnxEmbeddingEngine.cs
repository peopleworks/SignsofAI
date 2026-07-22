using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using SignsOfAI.Perplexity.Api.Config;
using Tokenizers.DotNet;

namespace SignsOfAI.Perplexity.Api.Engine;

/// <summary>
/// One selectable sentence-embedding model. Produces L2-normalized (optionally Matryoshka-truncated) vectors
/// via ONNX Runtime (CPU) + the HF tokenizer for its <see cref="EmbeddingProfile"/>. It reuses the same
/// lifecycle as the perplexity engine — <b>lazily loaded</b> on first use and <b>idle-unloaded</b> from RAM —
/// so the 300M model only costs memory while it's actually being used. This is deliberately a separate class
/// from the perplexity engine so the live perplexity path is never at risk from embedding changes.
/// </summary>
public sealed class OnnxEmbeddingEngine(EmbeddingProfile profile, IHostEnvironment env, ILogger log) : IDisposable
{
    private readonly string _dir = Path.IsPathRooted(profile.ModelDir) ? profile.ModelDir : Path.Combine(env.ContentRootPath, profile.ModelDir);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _downloadLock = new();
    private Task? _downloadTask;
    private InferenceSession? _session;
    private Tokenizer? _tokenizer;
    private string? _outputName;
    private int _active;
    private DateTime _lastUsedUtc = DateTime.UtcNow;
    private volatile bool _filesReady;
    private volatile bool _loaded;

    public EmbeddingProfile Profile => profile;
    public string ModelId => profile.Id;
    public bool IsLoaded => _loaded;
    public bool FilesReady => _filesReady;

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
            // CancellationToken.None on purpose: a disconnecting client must not abort a multi-minute download.
            await EnsureFileAsync(modelPath, profile.ModelUrl, CancellationToken.None);
            await EnsureFileAsync(tokPath, profile.TokenizerUrl, CancellationToken.None);
            foreach (var url in profile.AuxFileUrls)
                await EnsureFileAsync(Path.Combine(_dir, Path.GetFileName(new Uri(url).AbsolutePath)), url, CancellationToken.None);

            _filesReady = File.Exists(modelPath) && File.Exists(tokPath);
            if (!_filesReady) log.LogError("[embed:{Model}] model/tokenizer missing at {Dir} and no download URL configured.", profile.Id, _dir);
            else log.LogInformation("[embed:{Model}] files ready.", profile.Id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "[embed:{Model}] download failed; will retry on next request.", profile.Id);
            lock (_downloadLock) _downloadTask = null;
        }
    }

    /// <summary>Embeds each text into an L2-normalized vector of <paramref name="dims"/> components.</summary>
    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, int dims, CancellationToken ct = default)
    {
        int d = Math.Clamp(dims <= 0 ? profile.DefaultDims : dims, 32, profile.OutputDim);
        var (session, tokenizer, outName) = await AcquireAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var outp = new float[texts.Count][];
                for (int i = 0; i < texts.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    outp[i] = EmbedOne(session, tokenizer, outName, texts[i], d);
                }
                return outp;
            }, ct);
        }
        finally { Release(); }
    }

    public async Task WarmupAsync(CancellationToken ct = default)
    {
        _ = await AcquireAsync(ct);
        Release();
    }

    private async Task<(InferenceSession, Tokenizer, string)> AcquireAsync(CancellationToken ct)
    {
        if (!_filesReady)
        {
            _ = GetOrStartDownload();
            throw new InvalidOperationException($"Embedding model '{profile.Id}' is downloading on the server; retry shortly.");
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_session is null) LoadLocked();
            _active++;
            return (_session!, _tokenizer!, _outputName!);
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
        // Pick the pooled sentence-embedding output ("sentence_embedding"); fall back to the last output.
        _outputName = _session.OutputMetadata.Keys.FirstOrDefault(k => k.Contains("sentence", StringComparison.OrdinalIgnoreCase))
                      ?? _session.OutputMetadata.Keys.Last();
        _loaded = true;
        log.LogInformation("[embed:{Model}] loaded into RAM in {Ms} ms (output '{Out}').", profile.Id, sw.ElapsedMilliseconds, _outputName);
    }

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

    private float[] EmbedOne(InferenceSession session, Tokenizer tokenizer, string outName, string text, int dims)
    {
        var ids = Array.ConvertAll(tokenizer.Encode(profile.QueryPrefix + (text ?? "")), x => (long)x);
        if (ids.Length == 0) ids = [0L];
        if (ids.Length > profile.MaxTokens) ids = ids[..profile.MaxTokens];
        int n = ids.Length;

        var toDispose = new List<OrtValue>(4);
        OrtValue Mk<T>(T[] data, long[] shape) where T : unmanaged
        { var v = OrtValue.CreateTensorValueFromMemory(data, shape); toDispose.Add(v); return v; }

        try
        {
            var inputs = new Dictionary<string, OrtValue>(4) { ["input_ids"] = Mk(ids, [1, n]) };
            var attn = new long[n]; Array.Fill(attn, 1L);
            inputs["attention_mask"] = Mk(attn, [1, n]);
            // Add these only if this particular export declares them (Gemma variants differ).
            if (session.InputMetadata.ContainsKey("token_type_ids"))
                inputs["token_type_ids"] = Mk(new long[n], [1, n]);
            if (session.InputMetadata.ContainsKey("position_ids"))
            {
                var pos = new long[n]; for (int i = 0; i < n; i++) pos[i] = i;
                inputs["position_ids"] = Mk(pos, [1, n]);
            }

            using var ro = new RunOptions();
            using var results = session.Run(ro, inputs, [outName]);
            var vec = results[0].GetTensorDataAsSpan<float>();

            int d = Math.Min(dims, vec.Length);
            var outv = new float[d];
            for (int i = 0; i < d; i++) outv[i] = vec[i];

            // Matryoshka truncation ⇒ re-normalize to unit length so cosine == dot product downstream.
            double norm = 0; for (int i = 0; i < d; i++) norm += outv[i] * (double)outv[i];
            norm = Math.Sqrt(norm);
            if (norm > 1e-12) for (int i = 0; i < d; i++) outv[i] = (float)(outv[i] / norm);
            return outv;
        }
        finally { foreach (var x in toDispose) x.Dispose(); }
    }

    private async Task EnsureFileAsync(string path, string? url, CancellationToken ct)
    {
        if (File.Exists(path) || string.IsNullOrWhiteSpace(url)) return;
        log.LogInformation("[embed:{Model}] downloading {Url} …", profile.Id, url);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var tmp = path + ".part";
        await using (var fs = File.Create(tmp)) await resp.Content.CopyToAsync(fs, ct);
        File.Move(tmp, path, overwrite: true);
        log.LogInformation("[embed:{Model}] downloaded {Path} ({Bytes:N0} bytes).", profile.Id, Path.GetFileName(path), new FileInfo(path).Length);
    }

    public void Dispose() { _session?.Dispose(); _tokenizer?.Dispose(); _gate.Dispose(); }
}

/// <summary>Holds one <see cref="OnnxEmbeddingEngine"/> per configured embedding model and resolves requests.</summary>
public sealed class EmbeddingRegistry
{
    private readonly Dictionary<string, OnnxEmbeddingEngine> _byId = new(StringComparer.OrdinalIgnoreCase);
    public bool Enabled { get; }
    public OnnxEmbeddingEngine? Default { get; }
    public IReadOnlyCollection<OnnxEmbeddingEngine> Engines => _byId.Values;

    public EmbeddingRegistry(EmbeddingOptions options, IHostEnvironment env, ILoggerFactory lf)
    {
        Enabled = options.Enabled && options.Models.Count > 0;
        if (!Enabled) return;
        foreach (var p in options.Models)
            _byId[p.Id] = new OnnxEmbeddingEngine(p, env, lf.CreateLogger($"Embedding.{p.Id}"));
        var defId = options.DefaultModel ?? options.Models.FirstOrDefault()?.Id;
        Default = (defId is not null && _byId.TryGetValue(defId, out var d)) ? d : _byId.Values.First();
    }

    /// <summary>Resolves the requested model id, falling back to the default when null/blank/unknown.</summary>
    public OnnxEmbeddingEngine? Resolve(string? modelId) =>
        !string.IsNullOrWhiteSpace(modelId) && _byId.TryGetValue(modelId, out var e) ? e : Default;
}

/// <summary>At startup ensures the default embedding model's file is on disk; then idle-unloads it when unused.</summary>
public sealed class EmbeddingLifecycleService(EmbeddingRegistry registry, EmbeddingOptions options, ILogger<EmbeddingLifecycleService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!registry.Enabled || registry.Default is null) return;

        try { await registry.Default.EnsureFilesAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { log.LogError(ex, "Default embedding model failed to initialize."); }

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
                    log.LogInformation("[embed:{Model}] unloaded after {Idle}s idle — RAM freed.", e.ModelId, options.IdleUnloadSeconds);
        }
    }
}

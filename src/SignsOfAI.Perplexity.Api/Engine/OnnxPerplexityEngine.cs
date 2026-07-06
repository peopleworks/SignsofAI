using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using SignsOfAI.Perplexity.Api.Config;
using Tokenizers.DotNet;

namespace SignsOfAI.Perplexity.Api.Engine;

/// <summary>
/// Computes causal-LM perplexity with a Qwen2.5-0.5B ONNX model via ONNX Runtime (CPU) and the
/// Hugging Face tokenizer. The model is a merged (with-past) export, so a from-scratch forward pass
/// supplies empty KV-cache tensors. Loaded once (see <see cref="InitializeAsync"/>) and reused;
/// <see cref="InferenceSession.Run(RunOptions, IReadOnlyDictionary{string, OrtValue}, IReadOnlyCollection{string})"/>
/// is thread-safe, and each call builds its own inputs, so concurrent scoring is safe.
/// </summary>
public sealed class OnnxPerplexityEngine(PerplexityOptions options, IHostEnvironment env, ILogger<OnnxPerplexityEngine> log)
    : IPerplexityEngine, IDisposable
{
    private readonly PerplexityOptions _o = options;
    private InferenceSession? _session;
    private Tokenizer? _tokenizer;
    private volatile bool _ready;

    public string ModelId => _o.ModelId;
    public bool IsReady => _ready;

    /// <summary>Resolves + downloads the model/tokenizer if needed, then loads them. Call once at startup.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var dir = Path.IsPathRooted(_o.ModelDir) ? _o.ModelDir : Path.Combine(env.ContentRootPath, _o.ModelDir);
        Directory.CreateDirectory(dir);
        var modelPath = Path.Combine(dir, _o.ModelFile);
        var tokPath = Path.Combine(dir, _o.TokenizerFile);

        await EnsureFileAsync(modelPath, _o.ModelUrl, ct);
        await EnsureFileAsync(tokPath, _o.TokenizerUrl, ct);

        if (!File.Exists(modelPath) || !File.Exists(tokPath))
        {
            log.LogError("Perplexity model/tokenizer missing at {Dir} and no download URL configured; engine stays not-ready.", dir);
            return;
        }

        var so = new Microsoft.ML.OnnxRuntime.SessionOptions();
        if (_o.IntraOpThreads > 0) so.IntraOpNumThreads = _o.IntraOpThreads;

        var sw = Stopwatch.StartNew();
        _tokenizer = new Tokenizer(tokPath);
        _session = new InferenceSession(modelPath, so);
        sw.Stop();

        _ready = true;
        log.LogInformation("Perplexity engine ready: {Model} loaded in {Ms} ms.", _o.ModelFile, sw.ElapsedMilliseconds);
    }

    public Task<PerplexityRaw> ScoreAsync(string text, CancellationToken ct = default)
    {
        if (!_ready || _session is null || _tokenizer is null)
            throw new InvalidOperationException("Perplexity engine is not ready.");

        // CPU-bound work; hand off so we don't block the request pipeline thread.
        return Task.Run(() =>
        {
            var ids = Array.ConvertAll(_tokenizer.Encode(text), x => (long)x);
            if (ids.Length > _o.MaxTokens)
                ids = ids[.._o.MaxTokens]; // right-truncate for bounded latency

            if (ids.Length < 2)
                return new PerplexityRaw(1.0, 0.0, 0, 0);

            var sw = Stopwatch.StartNew();
            var (ppl, meanLogProb, scored) = Forward(ids, ct);
            sw.Stop();
            return new PerplexityRaw(ppl, meanLogProb, scored, sw.ElapsedMilliseconds);
        }, ct);
    }

    private (double ppl, double meanLogProb, int scored) Forward(long[] ids, CancellationToken ct)
    {
        int n = ids.Length, vocab = _o.Vocab;
        var toDispose = new List<OrtValue>(2 * _o.NumLayers + 3);
        OrtValue Mk<T>(T[] data, long[] shape) where T : unmanaged
        { var v = OrtValue.CreateTensorValueFromMemory(data, shape); toDispose.Add(v); return v; }

        try
        {
            var inputs = new Dictionary<string, OrtValue>(2 * _o.NumLayers + 3)
            {
                ["input_ids"] = Mk(ids, [1, n]),
            };
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
            using var results = _session!.Run(ro, inputs, ["logits"]);
            var logits = results[0].GetTensorDataAsSpan<float>(); // [1, n, vocab] row-major

            // Shift: logits at position t predict token ids[t+1]. Score t = 0..n-2.
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

            double meanNll = sumNll / scored;
            return (Math.Exp(meanNll), -meanNll, scored);
        }
        finally
        {
            foreach (var d in toDispose) d.Dispose();
        }
    }

    private async Task EnsureFileAsync(string path, string? url, CancellationToken ct)
    {
        if (File.Exists(path) || string.IsNullOrWhiteSpace(url)) return;
        log.LogInformation("Downloading {Url} → {Path} …", url, path);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var tmp = path + ".part";
        await using (var fs = File.Create(tmp))
            await resp.Content.CopyToAsync(fs, ct);
        File.Move(tmp, path, overwrite: true);
        log.LogInformation("Downloaded {Path} ({Bytes:N0} bytes).", path, new FileInfo(path).Length);
    }

    public void Dispose() => _session?.Dispose();
}

/// <summary>Loads the model at startup (downloading if needed) without blocking app boot.</summary>
public sealed class ModelWarmupService(OnnxPerplexityEngine engine, ILogger<ModelWarmupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await engine.InitializeAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { log.LogError(ex, "Perplexity engine failed to initialize."); }
    }
}

using Microsoft.AspNetCore.Http.Json;
using SignsOfAI.Perplexity.Api.Config;
using SignsOfAI.Perplexity.Api.Engine;
using SignsOfAI.Perplexity.Api.Model;
using SignsOfAI.Perplexity.Api.Scoring;

var builder = WebApplication.CreateBuilder(args);

// ── Options (models + calibration + idle policy in the "Perplexity" section) ──
var options = builder.Configuration.GetSection("Perplexity").Get<PerplexityOptions>();
if (options is null || options.Models.Count == 0)
    options = PerplexityOptions.Defaults();
builder.Services.AddSingleton(options);

// One heavyweight engine per model (each lazy-loads + idle-unloads independently). Run is thread-safe.
builder.Services.AddSingleton<PerplexityRegistry>();
builder.Services.AddHostedService<ModelLifecycleService>();

// ── Embedding subsystem (optional paraphrase/semantic-similarity check) ──
var embedOptions = builder.Configuration.GetSection("Embedding").Get<EmbeddingOptions>() ?? EmbeddingOptions.Defaults();
builder.Services.AddSingleton(embedOptions);
builder.Services.AddSingleton<EmbeddingRegistry>();
builder.Services.AddHostedService<EmbeddingLifecycleService>();

// ── Optional automatic web spot-check (Phase D+) — inert unless an operator configured a provider + key ──
var webSearchOptions = builder.Configuration.GetSection("WebSearch").Get<WebSearchOptions>() ?? new WebSearchOptions();
builder.Services.AddSingleton(webSearchOptions);
builder.Services.AddSingleton<SignsOfAI.Perplexity.Api.Search.WebSearchService>();

// Source-generated JSON so we stay trim/AOT-friendly.
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));

// ── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["https://peopleworks.github.io", "http://localhost:5019", "https://localhost:5019"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST")));

var app = builder.Build();
app.UseCors();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapGet("/", (PerplexityRegistry reg, EmbeddingRegistry embed, SignsOfAI.Perplexity.Api.Search.WebSearchService web) => Results.Ok(new ServiceInfo
{
    ModelReady = reg.Default.FilesReady,
    Languages = [.. reg.Default.Profile.Baselines.Keys],
    Models = [.. reg.Engines.Select(e => new ModelInfo
    {
        Id = e.ModelId, Label = e.Profile.Label, Note = e.Profile.Note,
        IsDefault = e == reg.Default, Loaded = e.IsLoaded,
    })],
    EmbeddingReady = embed.Default?.FilesReady ?? false,
    EmbeddingModels = [.. embed.Engines.Select(e => new ModelInfo
    {
        Id = e.ModelId, Label = e.Profile.Label, Note = e.Profile.Note,
        IsDefault = e == embed.Default, Loaded = e.IsLoaded,
    })],
    WebSearchReady = web.IsActive,
}));

app.MapGet("/healthz", (PerplexityRegistry reg) =>
    reg.Default.FilesReady ? Results.Ok("ready") : Results.StatusCode(503));

// Cheap pre-warm so the model is in RAM before the user measures (hides idle cold-reload).
app.MapGet("/api/warmup", async (string? model, PerplexityRegistry reg, CancellationToken ct) =>
{
    var engine = reg.Resolve(model);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try { await engine.WarmupAsync(ct); }
    catch { return Results.Json(new { model = engine.ModelId, loaded = false }); }
    return Results.Json(new { model = engine.ModelId, loaded = engine.IsLoaded, elapsedMs = sw.ElapsedMilliseconds });
});

app.MapPost("/api/perplexity", async (PerplexityRequest req, PerplexityRegistry reg, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "text is required" });
    if (req.Text.Length > 20_000)
        return Results.BadRequest(new { error = "text exceeds 20000 characters" });

    var engine = reg.Resolve(req.Model);
    try
    {
        var raw = await engine.ScoreAsync(req.Text, ct);
        return Results.Ok(PerplexityScorer.Score(raw, req.Lang, engine.Profile));
    }
    catch (InvalidOperationException)
    {
        return Results.Json(new { error = $"model '{engine.ModelId}' is warming up or downloading, retry shortly" }, statusCode: 503);
    }
});

// ── Embedding endpoints (paraphrase / semantic-similarity check) ──
app.MapGet("/api/embed/warmup", async (string? model, EmbeddingRegistry embed, CancellationToken ct) =>
{
    var engine = embed.Resolve(model);
    if (engine is null) return Results.Json(new { enabled = false });
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try { await engine.WarmupAsync(ct); }
    catch { return Results.Json(new { model = engine.ModelId, loaded = false }); }
    return Results.Json(new { model = engine.ModelId, loaded = engine.IsLoaded, elapsedMs = sw.ElapsedMilliseconds });
});

app.MapPost("/api/embed", async (EmbedRequest req, EmbeddingRegistry embed, CancellationToken ct) =>
{
    var engine = embed.Resolve(req.Model);
    if (engine is null) return Results.Json(new { error = "embedding feature is disabled" }, statusCode: 404);
    if (req.Texts is null || req.Texts.Length == 0)
        return Results.BadRequest(new { error = "texts is required" });
    if (req.Texts.Length > 1024)
        return Results.BadRequest(new { error = "too many texts (max 1024)" });
    long totalChars = req.Texts.Sum(t => (long)(t?.Length ?? 0));
    if (totalChars > 400_000)
        return Results.BadRequest(new { error = "texts exceed 400000 characters total" });

    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        int dims = req.Dims ?? engine.Profile.DefaultDims;
        var vectors = await engine.EmbedAsync(req.Texts, dims, ct);
        return Results.Ok(new EmbedResponse
        {
            Model = engine.ModelId,
            Dims = vectors.Length > 0 ? vectors[0].Length : dims,
            Vectors = vectors,
            ElapsedMs = sw.ElapsedMilliseconds,
        });
    }
    catch (InvalidOperationException)
    {
        return Results.Json(new { error = $"embedding model '{engine.ModelId}' is warming up or downloading, retry shortly" }, statusCode: 503);
    }
});

// ── Optional automatic web spot-check (Phase D+) ──
app.MapPost("/api/webcheck", async (WebCheckRequest req, SignsOfAI.Perplexity.Api.Search.WebSearchService web, CancellationToken ct) =>
{
    if (!web.IsActive) return Results.Json(new { enabled = false }, statusCode: 404);
    if (req.Phrases is null || req.Phrases.Length == 0)
        return Results.BadRequest(new { error = "phrases is required" });

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var results = await web.CheckAsync(req.Phrases, ct);
    return Results.Ok(new WebCheckResponse { Results = [.. results], ElapsedMs = sw.ElapsedMilliseconds });
});

app.Run();

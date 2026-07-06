using Microsoft.AspNetCore.Http.Json;
using SignsOfAI.Perplexity.Api.Config;
using SignsOfAI.Perplexity.Api.Engine;
using SignsOfAI.Perplexity.Api.Model;
using SignsOfAI.Perplexity.Api.Scoring;

var builder = WebApplication.CreateBuilder(args);

// ── Options (calibration + model config live in the "Perplexity" section) ────
var options = builder.Configuration.GetSection("Perplexity").Get<PerplexityOptions>();
if (options is null || options.Baselines.Count == 0)
    options = PerplexityOptions.Defaults();
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<PerplexityScorer>();

// Heavyweight singleton: model loaded once, reused (Run is thread-safe). Warmed up off the boot thread.
builder.Services.AddSingleton<OnnxPerplexityEngine>();
builder.Services.AddSingleton<IPerplexityEngine>(sp => sp.GetRequiredService<OnnxPerplexityEngine>());
builder.Services.AddHostedService<ModelLifecycleService>();

// Source-generated JSON so we stay trim/AOT-friendly.
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));

// ── CORS ─────────────────────────────────────────────────────────────────────
// The Blazor client is served from GitHub Pages and localhost during dev; both call this cross-origin.
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["https://peopleworks.github.io", "http://localhost:5019", "https://localhost:5019"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST")));

var app = builder.Build();
app.UseCors();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapGet("/", (IPerplexityEngine engine, PerplexityOptions opts) => Results.Ok(new ServiceInfo
{
    Model = engine.ModelId,
    ModelReady = engine.IsReady,
    ModelLoaded = engine.IsLoaded,
    Languages = [.. opts.Baselines.Keys],
}));

app.MapGet("/healthz", (IPerplexityEngine engine) =>
    engine.IsReady ? Results.Ok("ready") : Results.StatusCode(503));

// Cheap pre-warm: clients call this when the user is about to measure so the model is already
// in RAM (hides the ~1.5s cold reload after an idle-unload). Returns fast if already loaded.
app.MapGet("/api/warmup", async (IPerplexityEngine engine, CancellationToken ct) =>
{
    if (!engine.IsReady) return Results.Json(new { modelLoaded = false, ready = false });
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await engine.WarmupAsync(ct);
    return Results.Json(new { modelLoaded = engine.IsLoaded, ready = true, elapsedMs = sw.ElapsedMilliseconds });
});

app.MapPost("/api/perplexity", async (
    PerplexityRequest req, IPerplexityEngine engine, PerplexityScorer scorer, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "text is required" });
    if (req.Text.Length > 20_000)
        return Results.BadRequest(new { error = "text exceeds 20000 characters" });
    if (!engine.IsReady)
        return Results.Json(new { error = "model is warming up, retry shortly" }, statusCode: 503);

    var raw = await engine.ScoreAsync(req.Text, ct);
    return Results.Ok(scorer.Score(raw, req.Lang, engine.ModelId));
});

app.Run();

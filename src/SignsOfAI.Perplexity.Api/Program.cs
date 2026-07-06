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
app.MapGet("/", (PerplexityRegistry reg, PerplexityOptions opts) => Results.Ok(new ServiceInfo
{
    ModelReady = reg.Default.FilesReady,
    Languages = [.. reg.Default.Profile.Baselines.Keys],
    Models = [.. reg.Engines.Select(e => new ModelInfo
    {
        Id = e.ModelId, Label = e.Profile.Label, Note = e.Profile.Note,
        IsDefault = e == reg.Default, Loaded = e.IsLoaded,
    })],
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

app.Run();

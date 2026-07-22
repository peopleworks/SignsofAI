using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Server;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Originality;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Mcp.Tools;

/// <summary>
/// The two tools that reach the optional SignsOfAI server (perplexity + embeddings). Unlike the offline
/// tools, these SEND TEXT off the machine to the configured endpoint — the tool descriptions disclose it.
/// Endpoint comes from the SIGNSOFAI_API_ENDPOINT environment variable, defaulting to the hosted API.
/// </summary>
[McpServerToolType]
public static class ServerTools
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    private const string DefaultEndpoint = "https://signsofai.perplexity.api.peopleworksservices.com";

    private static string Endpoint =>
        Environment.GetEnvironmentVariable("SIGNSOFAI_API_ENDPOINT") is { Length: > 0 } e
            ? e.TrimEnd('/')
            : DefaultEndpoint;

    [McpServerTool(Name = "measure_predictability", ReadOnly = true, OpenWorld = true),
     Description("""
        Measures how PREDICTABLE (generic) a language model finds the phrasing — its perplexity. Predictable,
        generic wording is common in AI writing, but formulaic human text scores predictable too and stylized AI
        can score varied: it is a signal, not proof. NOTE: unlike the offline tools, this SENDS THE TEXT to the
        SignsOfAI server to run the model (endpoint from SIGNSOFAI_API_ENDPOINT; defaults to the hosted API).
        """)]
    public static async Task<PredictabilityResult> MeasurePredictability(
        [Description("The text to score.")] string text,
        [Description("Language: \"en\", \"es\", or \"auto\". Default \"auto\".")] string language = "auto",
        [Description("Optional model id (see the server's model list). Empty = server default.")] string? model = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is empty.");

        var body = new
        {
            text,
            lang = string.IsNullOrWhiteSpace(language) ? "auto" : language,
            model = string.IsNullOrWhiteSpace(model) ? null : model,
        };

        using var resp = await Post("/api/perplexity", body, ct);
        if (resp.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("The model is loading on the server (large models download on first use). Try again in a minute.");
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Server returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");

        var r = await resp.Content.ReadFromJsonAsync<PerplexityResponse>(ct)
                ?? throw new InvalidOperationException("Empty response from the server.");

        return new PredictabilityResult(
            Math.Round(r.Predictability * 100, 1), r.Band, Math.Round(r.Ppl, 1), r.TokenCount, r.Model, r.Lang, Endpoint);
    }

    [McpServerTool(Name = "check_paraphrase", ReadOnly = true, OpenWorld = true),
     Description("""
        Finds REWORDED copies between two texts — same meaning, different words, including across languages
        (e.g. English vs Spanish) — that a literal copy check can't see. It embeds each sentence and compares
        cosine similarity. NOTE: this SENDS BOTH TEXTS to the SignsOfAI server to embed them (endpoint from
        SIGNSOFAI_API_ENDPOINT). Requires the embedding feature to be enabled on the server.
        """)]
    public static async Task<ParaphraseResult> CheckParaphrase(
        [Description("First document.")] string textA,
        [Description("Second document.")] string textB,
        [Description("Cosine similarity threshold 0..1 to count as a paraphrase. Default 0.72.")] double threshold = 0.72,
        CancellationToken ct = default)
    {
        var docA = new TextDocument(textA ?? string.Empty);
        var docB = new TextDocument(textB ?? string.Empty);
        var spansA = docA.Sentences.Select(s => s.Span).ToList();
        var spansB = docB.Sentences.Select(s => s.Span).ToList();
        if (spansA.Count == 0 || spansB.Count == 0)
            return new ParaphraseResult(threshold, [], Endpoint);

        var textsA = spansA.Select(sp => docA.Raw.Substring(sp.Start, sp.Length)).ToArray();
        var textsB = spansB.Select(sp => docB.Raw.Substring(sp.Start, sp.Length)).ToArray();

        var vecA = await Embed(textsA, ct);
        var vecB = await Embed(textsB, ct);

        var matches = ParaphraseFinder.Find(spansA, vecA, spansB, vecB, threshold, Array.Empty<TextSpan>());
        var dtos = matches
            .Select(m => new ParaphraseMatchDto(
                docA.Raw.Substring(m.SpanA.Start, m.SpanA.Length),
                docB.Raw.Substring(m.SpanB.Start, m.SpanB.Length),
                Math.Round(m.Similarity, 3)))
            .ToList();

        return new ParaphraseResult(threshold, dtos, Endpoint);
    }

    private static async Task<float[][]> Embed(string[] texts, CancellationToken ct)
    {
        var body = new { texts, model = (string?)null, dims = (int?)null };
        using var resp = await Post("/api/embed", body, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("The paraphrase (embedding) feature isn't enabled on this server.");
        if (resp.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new InvalidOperationException("The embedding model is loading on the server. Try again in a minute.");
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Server returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");

        var r = await resp.Content.ReadFromJsonAsync<EmbedResponse>(ct)
                ?? throw new InvalidOperationException("Empty response from the embedding server.");
        return r.Vectors;
    }

    private static async Task<HttpResponseMessage> Post(string path, object body, CancellationToken ct)
    {
        try
        {
            return await Http.PostAsJsonAsync($"{Endpoint}{path}", body, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Couldn't reach the SignsOfAI server at {Endpoint}. {ex.Message}");
        }
    }

    private sealed record PerplexityResponse(
        double Ppl, double AvgLogProb, int TokenCount, double Predictability, string Band, string Model, string Lang, long ElapsedMs);

    private sealed record EmbedResponse(string Model, int Dims, float[][] Vectors, long ElapsedMs);
}

public sealed record PredictabilityResult(
    double PredictablePercent, string Band, double Perplexity, int Tokens, string Model, string Language, string Endpoint);

public sealed record ParaphraseMatchDto(string SentenceA, string SentenceB, double Similarity);

public sealed record ParaphraseResult(double Threshold, IReadOnlyList<ParaphraseMatchDto> Matches, string Endpoint);

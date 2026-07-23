using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Web.Services;

public enum AiProvider { Anthropic, OpenAI, AzureOpenAI, DeepSeek, Ollama }

/// <summary>User-configured provider settings, persisted in the browser's localStorage.</summary>
public sealed class HumanizeSettings
{
    [JsonPropertyName("provider")] public AiProvider Provider { get; set; } = AiProvider.Anthropic;
    [JsonPropertyName("apiKey")] public string ApiKey { get; set; } = string.Empty;
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;      // OpenAI / DeepSeek / Ollama
    [JsonPropertyName("azureEndpoint")] public string AzureEndpoint { get; set; } = string.Empty;
    [JsonPropertyName("azureDeployment")] public string AzureDeployment { get; set; } = string.Empty;
    [JsonPropertyName("azureApiVersion")] public string AzureApiVersion { get; set; } = "2024-10-21";
    [JsonPropertyName("ollamaBaseUrl")] public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>True when the active provider has everything it needs to make a call.</summary>
    public bool IsConfigured() => Provider switch
    {
        AiProvider.Anthropic => !string.IsNullOrWhiteSpace(ApiKey),
        AiProvider.OpenAI => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model),
        AiProvider.DeepSeek => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model),
        AiProvider.AzureOpenAI => !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(AzureEndpoint) && !string.IsNullOrWhiteSpace(AzureDeployment),
        AiProvider.Ollama => !string.IsNullOrWhiteSpace(OllamaBaseUrl) && !string.IsNullOrWhiteSpace(Model),
        _ => false,
    };
}

public sealed record HumanizeResult(bool Success, string Text, string? Error);

/// <summary>
/// Optional "Humanize" feature. Rewrites text to remove AI tells, calling an LLM directly from the
/// browser (BYOK — the key never leaves the device, there is no backend). Providers:
///  • Anthropic (claude-opus-4-8) — works from the browser via the direct-access header.
///  • OpenAI / DeepSeek — OpenAI-style chat completions; the provider must allow browser CORS.
///  • Azure OpenAI — your own Azure AI resource; requires CORS allowed on the resource.
///  • Ollama — a local model on your machine; works best when SignsOfAI itself runs locally.
/// </summary>
public sealed class HumanizerService(HttpClient http)
{
    private const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicModel = "claude-opus-4-8";
    private const int MaxTokens = 8000;

    public Task<HumanizeResult> HumanizeAsync(
        string text, string language, IReadOnlyList<Finding> findings, HumanizeSettings s,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(new HumanizeResult(false, string.Empty, "Nothing to humanize."));

        var system = BuildSystemPrompt(language);
        var user = BuildUserPrompt(text, findings);

        return s.Provider switch
        {
            AiProvider.Anthropic => AnthropicAsync(system, user, s, ct),
            AiProvider.AzureOpenAI => AzureAsync(system, user, s, ct),
            AiProvider.OpenAI => OpenAiStyleAsync(
                "https://api.openai.com/v1/chat/completions", s.ApiKey, s.Model, system, user, "OpenAI", ct),
            AiProvider.DeepSeek => OpenAiStyleAsync(
                "https://api.deepseek.com/v1/chat/completions", s.ApiKey, s.Model, system, user, "DeepSeek", ct),
            AiProvider.Ollama => OpenAiStyleAsync(
                $"{s.OllamaBaseUrl.TrimEnd('/')}/v1/chat/completions", null, s.Model, system, user, "Ollama", ct),
            _ => Task.FromResult(new HumanizeResult(false, string.Empty, "Unknown provider.")),
        };
    }

    private async Task<HumanizeResult> AnthropicAsync(
        string system, string user, HumanizeSettings s, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.ApiKey))
            return new HumanizeResult(false, string.Empty, "Add your Anthropic API key first.");

        var request = new AnthropicRequest
        {
            Model = AnthropicModel,
            MaxTokens = MaxTokens,
            System = system,
            Messages = [new ChatMessage { Role = "user", Content = user }],
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, AnthropicEndpoint)
        {
            Content = JsonContent.Create(request, LlmJsonContext.Default.AnthropicRequest),
        };
        msg.Headers.Add("x-api-key", s.ApiKey);
        msg.Headers.Add("anthropic-version", "2023-06-01");
        msg.Headers.Add("anthropic-dangerous-direct-browser-access", "true");

        try
        {
            using var response = await http.SendAsync(msg, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize(body, LlmJsonContext.Default.AnthropicResponse);

            if (!response.IsSuccessStatusCode || parsed?.Error is not null)
                return Fail(parsed?.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

            var outText = parsed?.Content?.FirstOrDefault(b => b.Type == "text")?.Text?.Trim();
            return string.IsNullOrEmpty(outText) ? Fail("Claude returned an empty response.") : Ok(outText);
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    private async Task<HumanizeResult> AzureAsync(
        string system, string user, HumanizeSettings s, CancellationToken ct)
    {
        var endpoint = s.AzureEndpoint.TrimEnd('/');
        var url = $"{endpoint}/openai/deployments/{s.AzureDeployment}/chat/completions?api-version={s.AzureApiVersion}";

        // Azure puts the model in the URL (deployment), so leave Model null.
        var request = new ChatRequest
        {
            Model = null,
            MaxTokens = MaxTokens,
            Messages =
            [
                new ChatMessage { Role = "system", Content = system },
                new ChatMessage { Role = "user", Content = user },
            ],
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, LlmJsonContext.Default.ChatRequest),
        };
        msg.Headers.Add("api-key", s.ApiKey);

        return await SendChatAsync(msg, "Azure OpenAI",
            "if this is a network/CORS error, allow this site's origin on your Azure OpenAI resource.", ct);
    }

    private async Task<HumanizeResult> OpenAiStyleAsync(
        string url, string? apiKey, string model, string system, string user, string label, CancellationToken ct)
    {
        var request = new ChatRequest
        {
            Model = model,
            MaxTokens = MaxTokens,
            Messages =
            [
                new ChatMessage { Role = "system", Content = system },
                new ChatMessage { Role = "user", Content = user },
            ],
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, LlmJsonContext.Default.ChatRequest),
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            msg.Headers.Add("Authorization", $"Bearer {apiKey}");

        var corsHint = label == "Ollama"
            ? "run Ollama with OLLAMA_ORIGINS allowing this site, and note that a hosted HTTPS page may be blocked from calling http://localhost — this works best when you run SignsOfAI locally."
            : $"{label} may not allow direct browser calls; a CORS/network error means you'd need a small proxy.";

        return await SendChatAsync(msg, label, corsHint, ct);
    }

    private async Task<HumanizeResult> SendChatAsync(
        HttpRequestMessage msg, string label, string corsHint, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(msg, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize(body, LlmJsonContext.Default.ChatResponse);

            if (!response.IsSuccessStatusCode || parsed?.Error is not null)
                return Fail(parsed?.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

            var outText = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrEmpty(outText) ? Fail($"{label} returned an empty response.") : Ok(outText);
        }
        catch (Exception ex)
        {
            return Fail($"{ex.Message} — {corsHint}");
        }
    }

    private static HumanizeResult Ok(string text) => new(true, text, null);
    private static HumanizeResult Fail(string error) => new(false, string.Empty, error);

    private static string BuildSystemPrompt(string language)
    {
        var lang = language == "es" ? "Spanish" : "English";
        return
            $"You are an expert editor who rewrites AI-generated prose so it reads as authentically human. " +
            $"Rewrite the user's text in {lang}, removing the tells of AI writing: overused vocabulary " +
            "(delve, tapestry, multifaceted, nuanced, underscore, pivotal, robust, foster, leverage, and their " +
            "Spanish analogues), rhetorical crutches (the rule of three, negative parallelisms like \"it's not " +
            "just X, it's Y\", false ranges, and hedging fillers such as \"it's worth noting that\"), copula " +
            "avoidance (\"serves as\", \"stands as a testament to\"), throat-clearing openers (\"here's the thing\", " +
            "\"let me be clear\"), weasel attribution (\"experts agree\", \"studies show\"), empty intensifiers " +
            "(just, simply, actually, truly), over-reliance on em-dashes, and uniform sentence rhythm. Deliberately " +
            "vary sentence length — follow a long, clause-heavy sentence with a short punchy one, so the cadence " +
            "breathes (high burstiness). Preserve the original meaning, facts, and language exactly. Do not add " +
            "new claims or invent details. Keep roughly the same length. Return ONLY the rewritten text — no " +
            "preamble, no commentary, no markdown fences.";
    }

    private static string BuildUserPrompt(string text, IReadOnlyList<Finding> findings)
    {
        var flagged = findings
            .Where(f => !string.IsNullOrWhiteSpace(f.MatchedText))
            .Select(f => f.MatchedText!)
            .Distinct()
            .Take(20)
            .ToList();

        var hint = flagged.Count > 0
            ? "\n\nPay particular attention to these flagged phrases: " + string.Join("; ", flagged) + "."
            : string.Empty;

        return $"Rewrite the following text to sound human:\n\n{text}{hint}";
    }
}

// ---- wire DTOs (source-generated for trim-safe WASM) ----

internal sealed class ChatMessage
{
    [JsonPropertyName("role")] public required string Role { get; init; }
    [JsonPropertyName("content")] public required string Content { get; init; }
}

internal sealed class AnthropicRequest
{
    [JsonPropertyName("model")] public required string Model { get; init; }
    [JsonPropertyName("max_tokens")] public required int MaxTokens { get; init; }
    [JsonPropertyName("system")] public required string System { get; init; }
    [JsonPropertyName("messages")] public required ChatMessage[] Messages { get; init; }
}

internal sealed class AnthropicResponse
{
    [JsonPropertyName("content")] public AnthropicContentBlock[]? Content { get; init; }
    [JsonPropertyName("error")] public LlmError? Error { get; init; }
}

internal sealed class AnthropicContentBlock
{
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
}

internal sealed class ChatRequest
{
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("messages")] public required ChatMessage[] Messages { get; init; }
    [JsonPropertyName("max_tokens")] public required int MaxTokens { get; init; }
}

internal sealed class ChatResponse
{
    [JsonPropertyName("choices")] public ChatChoice[]? Choices { get; init; }
    [JsonPropertyName("error")] public LlmError? Error { get; init; }
}

internal sealed class ChatChoice
{
    [JsonPropertyName("message")] public ChatMessageOut? Message { get; init; }
}

internal sealed class ChatMessageOut
{
    [JsonPropertyName("content")] public string? Content { get; init; }
}

internal sealed class LlmError
{
    [JsonPropertyName("message")] public string? Message { get; init; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AnthropicRequest))]
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(HumanizeSettings))]
internal partial class LlmJsonContext : JsonSerializerContext;

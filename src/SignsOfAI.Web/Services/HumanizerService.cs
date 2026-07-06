using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Web.Services;

public enum AiProvider { Anthropic, AzureOpenAI }

/// <summary>User-configured provider settings, persisted in the browser's localStorage.</summary>
public sealed class HumanizeSettings
{
    [JsonPropertyName("provider")] public AiProvider Provider { get; set; } = AiProvider.Anthropic;
    [JsonPropertyName("apiKey")] public string ApiKey { get; set; } = string.Empty;
    [JsonPropertyName("azureEndpoint")] public string AzureEndpoint { get; set; } = string.Empty;
    [JsonPropertyName("azureDeployment")] public string AzureDeployment { get; set; } = string.Empty;
    [JsonPropertyName("azureApiVersion")] public string AzureApiVersion { get; set; } = "2024-10-21";
}

public sealed record HumanizeResult(bool Success, string Text, string? Error);

/// <summary>
/// Optional "Humanize" feature. Rewrites text to remove AI tells, calling an LLM directly from the
/// browser (BYOK — the key never leaves the device, there is no backend). Two providers:
///  • Anthropic (claude-opus-4-8) — works from the browser out of the box via the direct-access header.
///  • Azure OpenAI — for the Microsoft/.NET crowd on their own Azure AI resource. Requires that the
///    resource allow the site's origin via CORS (or sit behind APIM/a Function), since Azure OpenAI
///    does not expose a browser-access header like Anthropic's.
/// </summary>
public sealed class HumanizerService(HttpClient http)
{
    private const string AnthropicEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicModel = "claude-opus-4-8";
    private const int MaxTokens = 8000;

    public Task<HumanizeResult> HumanizeAsync(
        string text, string language, IReadOnlyList<Finding> findings, HumanizeSettings settings,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(new HumanizeResult(false, string.Empty, "Nothing to humanize."));

        return settings.Provider switch
        {
            AiProvider.AzureOpenAI => AzureAsync(text, language, findings, settings, ct),
            _ => AnthropicAsync(text, language, findings, settings, ct),
        };
    }

    private async Task<HumanizeResult> AnthropicAsync(
        string text, string language, IReadOnlyList<Finding> findings, HumanizeSettings s, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.ApiKey))
            return new HumanizeResult(false, string.Empty, "Add your Anthropic API key first.");

        var request = new AnthropicRequest
        {
            Model = AnthropicModel,
            MaxTokens = MaxTokens,
            System = BuildSystemPrompt(language),
            Messages = [new ChatMessage { Role = "user", Content = BuildUserPrompt(text, findings) }],
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
                return new HumanizeResult(false, string.Empty,
                    parsed?.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

            var outText = parsed?.Content?.FirstOrDefault(b => b.Type == "text")?.Text?.Trim();
            return string.IsNullOrEmpty(outText)
                ? new HumanizeResult(false, string.Empty, "Claude returned an empty response.")
                : new HumanizeResult(true, outText, null);
        }
        catch (Exception ex) { return new HumanizeResult(false, string.Empty, ex.Message); }
    }

    private async Task<HumanizeResult> AzureAsync(
        string text, string language, IReadOnlyList<Finding> findings, HumanizeSettings s, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.ApiKey) || string.IsNullOrWhiteSpace(s.AzureEndpoint) ||
            string.IsNullOrWhiteSpace(s.AzureDeployment))
            return new HumanizeResult(false, string.Empty,
                "Azure needs an endpoint, a deployment name, and an API key.");

        var endpoint = s.AzureEndpoint.TrimEnd('/');
        var url = $"{endpoint}/openai/deployments/{s.AzureDeployment}/chat/completions?api-version={s.AzureApiVersion}";

        var request = new ChatRequest
        {
            MaxTokens = MaxTokens,
            Messages =
            [
                new ChatMessage { Role = "system", Content = BuildSystemPrompt(language) },
                new ChatMessage { Role = "user", Content = BuildUserPrompt(text, findings) },
            ],
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, LlmJsonContext.Default.ChatRequest),
        };
        msg.Headers.Add("api-key", s.ApiKey);

        try
        {
            using var response = await http.SendAsync(msg, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize(body, LlmJsonContext.Default.ChatResponse);

            if (!response.IsSuccessStatusCode || parsed?.Error is not null)
                return new HumanizeResult(false, string.Empty,
                    parsed?.Error?.Message ?? $"HTTP {(int)response.StatusCode}");

            var outText = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrEmpty(outText)
                ? new HumanizeResult(false, string.Empty, "Azure OpenAI returned an empty response.")
                : new HumanizeResult(true, outText, null);
        }
        catch (Exception ex)
        {
            // A bare network failure from the browser is almost always CORS on the Azure resource.
            return new HumanizeResult(false, string.Empty,
                $"{ex.Message} — if this is a network/CORS error, allow this site's origin on your Azure OpenAI resource.");
        }
    }

    private static string BuildSystemPrompt(string language)
    {
        var lang = language == "es" ? "Spanish" : "English";
        return
            $"You are an expert editor who rewrites AI-generated prose so it reads as authentically human. " +
            $"Rewrite the user's text in {lang}, removing the tells of AI writing: overused vocabulary " +
            "(delve, tapestry, multifaceted, nuanced, underscore, pivotal, robust, foster, leverage, and their " +
            "Spanish analogues), rhetorical crutches (the rule of three, negative parallelisms like \"it's not " +
            "just X, it's Y\", false ranges, and hedging fillers such as \"it's worth noting that\"), copula " +
            "avoidance (\"serves as\", \"stands as a testament to\"), and uniform sentence rhythm. Deliberately " +
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

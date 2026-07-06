using System.Text.Json.Serialization;

namespace SignsOfAI.Web.Services;

/// <summary>Summary data passed to the JS canvas renderer for the shareable card (no analyzed text).</summary>
public sealed record ShareCardData(
    double Score,
    string ScoreColor,
    string Verdict,
    int Signals,
    string Language,
    CardCategory[] Categories,
    int Words,
    int Sentences,
    string Burstiness,
    int[] Lengths);

public sealed record CardCategory(string Label, int Count, string Color);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ShareCardData))]
internal partial class ShareCardJsonContext : JsonSerializerContext;

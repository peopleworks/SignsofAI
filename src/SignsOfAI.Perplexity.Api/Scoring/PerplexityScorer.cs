using SignsOfAI.Perplexity.Api.Config;
using SignsOfAI.Perplexity.Api.Engine;
using SignsOfAI.Perplexity.Api.Model;

namespace SignsOfAI.Perplexity.Api.Scoring;

/// <summary>
/// Turns a raw perplexity into a calibrated, language-aware <b>predictability</b> reading. Low perplexity
/// ⇒ predictable/generic phrasing (common in AI text, but also in formulaic/memorized human text) ⇒ high
/// predictability. Deliberately NOT an AI-vs-human verdict — perplexity doesn't separate those reliably.
/// </summary>
public sealed class PerplexityScorer(PerplexityOptions options)
{
    private readonly PerplexityOptions _options = options;

    public PerplexityResponse Score(PerplexityRaw raw, string lang, string modelId)
    {
        var cal = Resolve(lang, out var resolvedLang);
        var logPpl = Math.Log(Math.Max(raw.Perplexity, 1.0001));

        // Below the language center ⇒ more predictable ⇒ higher meter.
        var z = (cal.Center - logPpl) / Math.Max(cal.Spread, 1e-3);
        var predictability = 1.0 / (1.0 + Math.Exp(-cal.Steepness * z));

        var band = predictability >= _options.PredictableAbove ? "very-predictable"
                 : predictability <= _options.VariedBelow ? "varied"
                 : "typical";

        return new PerplexityResponse
        {
            Ppl = Math.Round(raw.Perplexity, 2),
            AvgLogProb = Math.Round(raw.MeanLogProb, 4),
            TokenCount = raw.ScoredTokens,
            Predictability = Math.Round(predictability, 4),
            Band = band,
            Model = modelId,
            Lang = resolvedLang,
            ElapsedMs = raw.ElapsedMs,
        };
    }

    private LangBaseline Resolve(string lang, out string resolvedLang)
    {
        var key = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant();
        if (key is "auto" or "") key = "en";
        if (key.Length > 2) key = key[..2];

        if (_options.Baselines.TryGetValue(key, out var b)) { resolvedLang = key; return b; }
        if (_options.Baselines.TryGetValue("en", out var en)) { resolvedLang = "en"; return en; }

        resolvedLang = key;
        return new LangBaseline { Center = 4.0 };
    }
}

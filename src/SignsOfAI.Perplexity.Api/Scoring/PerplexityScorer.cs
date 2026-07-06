using SignsOfAI.Perplexity.Api.Config;
using SignsOfAI.Perplexity.Api.Engine;
using SignsOfAI.Perplexity.Api.Model;

namespace SignsOfAI.Perplexity.Api.Scoring;

/// <summary>Turns a raw perplexity into a calibrated, language-aware AI-likelihood verdict.</summary>
public sealed class PerplexityScorer(PerplexityOptions options)
{
    private readonly PerplexityOptions _options = options;

    public PerplexityResponse Score(PerplexityRaw raw, string lang, string modelId)
    {
        var baseline = Resolve(lang, out var resolvedLang);
        var logPpl = Math.Log(Math.Max(raw.Perplexity, 1.0001));

        // Distance in spreads BELOW the boundary. Below ⇒ machine-leaning ⇒ positive z ⇒ higher likelihood.
        var z = (baseline.BoundaryLogPpl - logPpl) / Math.Max(baseline.Spread, 1e-3);
        var likelihood = 1.0 / (1.0 + Math.Exp(-baseline.Steepness * z));

        var verdict = likelihood >= _options.AiThreshold ? "likely-ai"
                    : likelihood <= _options.HumanThreshold ? "likely-human"
                    : "uncertain";

        return new PerplexityResponse
        {
            Ppl = Math.Round(raw.Perplexity, 2),
            AvgLogProb = Math.Round(raw.MeanLogProb, 4),
            TokenCount = raw.ScoredTokens,
            ZScore = Math.Round(z, 3),
            AiLikelihood = Math.Round(likelihood, 4),
            Verdict = verdict,
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
        return new LangBaseline { BoundaryLogPpl = 4.1 };
    }
}

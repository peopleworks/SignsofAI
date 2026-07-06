using SignsOfAI.Perplexity.Api.Config;
using SignsOfAI.Perplexity.Api.Engine;
using SignsOfAI.Perplexity.Api.Model;

namespace SignsOfAI.Perplexity.Api.Scoring;

/// <summary>
/// Turns a raw perplexity into a calibrated, language-aware <b>predictability</b> reading using the model's
/// own calibration. Low perplexity ⇒ predictable/generic phrasing (common in AI text, but also in
/// formulaic/memorized human text) ⇒ high predictability. Deliberately NOT an AI-vs-human verdict.
/// </summary>
public static class PerplexityScorer
{
    public static PerplexityResponse Score(PerplexityRaw raw, string lang, ModelProfile profile)
    {
        var cal = profile.Baseline(lang);
        var resolvedLang = ResolveLang(lang, profile);
        var logPpl = Math.Log(Math.Max(raw.Perplexity, 1.0001));

        // Below the language center ⇒ more predictable ⇒ higher meter.
        var z = (cal.Center - logPpl) / Math.Max(cal.Spread, 1e-3);
        var predictability = 1.0 / (1.0 + Math.Exp(-cal.Steepness * z));

        var band = predictability >= profile.PredictableAbove ? "very-predictable"
                 : predictability <= profile.VariedBelow ? "varied"
                 : "typical";

        return new PerplexityResponse
        {
            Ppl = Math.Round(raw.Perplexity, 2),
            AvgLogProb = Math.Round(raw.MeanLogProb, 4),
            TokenCount = raw.ScoredTokens,
            Predictability = Math.Round(predictability, 4),
            Band = band,
            Model = profile.Id,
            Lang = resolvedLang,
            ElapsedMs = raw.ElapsedMs,
        };
    }

    private static string ResolveLang(string lang, ModelProfile profile)
    {
        var key = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant();
        if (key is "auto" or "") key = "en";
        if (key.Length > 2) key = key[..2];
        return profile.Baselines.ContainsKey(key) ? key : "en";
    }
}

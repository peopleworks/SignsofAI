using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using SignsOfAI.Core.Calibration;

namespace SignsOfAI.Calibration;

/// <summary>
/// Derives each rule's human usage rate from the corpus and writes it into the rule packs.
///
/// This exists because the first version of those numbers was computed in a throwaway script and
/// pasted in, which made the packs claim something the repository could not back up. A measured
/// threshold nobody can regenerate is a chosen threshold with better manners, and choosing thresholds
/// is the practice this project criticises in every article it has published.
/// </summary>
public static class Thresholds
{
    /// <summary>
    /// A rule needs this many texts before its rate means anything. The ninetieth percentile of eight
    /// samples is already close to the largest of them; below that it is one author's habit.
    /// </summary>
    public const int MinimumTexts = 8;

    /// <summary>The percentile of human usage a text must exceed before the rule counts as evidence.</summary>
    public const double Percentile = 0.90;

    /// <summary>Language → rule id → hits per thousand words at <see cref="Percentile"/>.</summary>
    public static Dictionary<string, Dictionary<string, double>> Derive(
        IReadOnlyList<CalibrationSample> samples)
    {
        var result = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);

        foreach (var language in samples.Select(s => s.Language).Distinct())
        {
            var texts = samples.Where(s => s.Language == language).ToList();
            var rates = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var ruleId in texts.SelectMany(t => t.MatchedRuleIds).Distinct())
            {
                // Statistical findings are one per document by construction, so a rate per thousand
                // words measures the document's length rather than the rule's behaviour.
                if (ruleId.StartsWith("stat.", StringComparison.Ordinal)) continue;

                if (texts.Count(t => t.MatchedRuleIds.Contains(ruleId)) < MinimumTexts) continue;

                var rate = RateAt(texts, ruleId, Percentile);
                if (rate > 0) rates[ruleId] = Math.Round(rate, 2);
            }

            if (rates.Count > 0) result[language] = rates;
        }

        return result;
    }

    /// <summary>
    /// The out-of-sample answer to "how much noise did this remove", and the only honest one: each
    /// text is judged against thresholds derived from the other texts, never from itself. Fitting on
    /// the corpus and then reporting the improvement on the same corpus is how a tool reports a
    /// number it cannot reproduce on anybody else's writing.
    /// </summary>
    public static (double Before, double After, int TextsCleanBefore, int TextsCleanAfter) LeaveOneOut(
        IReadOnlyList<CalibrationSample> samples)
    {
        double before = 0, after = 0;
        int cleanBefore = 0, cleanAfter = 0;

        foreach (var held in samples)
        {
            var others = samples.Where(s => s.Language == held.Language && !ReferenceEquals(s, held)).ToList();
            var rates = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var ruleId in others.SelectMany(t => t.MatchedRuleIds).Distinct())
            {
                if (ruleId.StartsWith("stat.", StringComparison.Ordinal)) continue;
                if (others.Count(t => t.MatchedRuleIds.Contains(ruleId)) < MinimumTexts) continue;
                var rate = RateAt(others, ruleId, Percentile);
                if (rate > 0) rates[ruleId] = Math.Round(rate, 2);
            }

            int kept = 0;
            foreach (var group in held.MatchedRuleIds.GroupBy(id => id, StringComparer.Ordinal))
            {
                var hits = group.Count();
                var rate = held.WordCount > 0 ? hits / (double)held.WordCount * 1000.0 : 0;
                if (!rates.TryGetValue(group.Key, out var threshold) || rate > threshold) kept += hits;
            }

            before += held.MatchedRuleIds.Count;
            after += kept;
            if (held.MatchedRuleIds.Count == 0) cleanBefore++;
            if (kept == 0) cleanAfter++;
        }

        return (before / samples.Count, after / samples.Count, cleanBefore, cleanAfter);
    }

    /// <summary>
    /// The rate at which <paramref name="ruleId"/> is used, at the given percentile, across every text
    /// in <paramref name="texts"/> — including the ones where it never fires, which count as zero. A
    /// percentile over only the texts that fired would answer "how heavily do the people who use this
    /// word use it", and the question here is "how often does this appear in writing at all".
    /// </summary>
    private static double RateAt(IReadOnlyList<CalibrationSample> texts, string ruleId, double percentile)
    {
        var rates = texts
            .Select(t => t.WordCount > 0
                ? t.MatchedRuleIds.Count(id => id == ruleId) / (double)t.WordCount * 1000.0
                : 0)
            .OrderBy(r => r)
            .ToList();

        var index = Math.Min(rates.Count - 1, (int)(rates.Count * percentile));
        return rates[index];
    }

    /// <summary>
    /// Writes the derived rates into a rule pack, adding <c>humanRatePer1000</c> to the rules that have
    /// one and removing it from those that no longer do — a rule that drops below
    /// <see cref="MinimumTexts"/> as the corpus changes must lose its threshold rather than keep a
    /// stale one. Returns how many rules were written.
    ///
    /// The file is rewritten from its parsed form, so formatting is normalised once and owned by this
    /// tool from then on. Non-ASCII is left as itself: the Spanish pack is full of it and escaping it
    /// would make every future diff unreadable.
    /// </summary>
    public static int WriteInto(string packPath, IReadOnlyDictionary<string, double> rates)
    {
        var root = JsonNode.Parse(File.ReadAllText(packPath))!.AsObject();
        int written = 0;

        foreach (var section in new[] { "lexical", "patterns" })
        {
            if (root[section] is not JsonArray rules) continue;

            foreach (var node in rules)
            {
                if (node is not JsonObject rule || rule["id"]?.GetValue<string>() is not { } id) continue;

                rule.Remove("humanRatePer1000");
                if (!rates.TryGetValue(id, out var rate)) continue;

                // Placed straight after the id so it reads as a property of the rule rather than an
                // afterthought appended to whatever the last field happened to be.
                var rebuilt = new JsonObject();
                foreach (var (key, value) in rule.ToList())
                {
                    rule.Remove(key);
                    rebuilt[key] = value;
                    if (key == "id") rebuilt["humanRatePer1000"] = rate;
                }

                foreach (var (key, value) in rebuilt.ToList())
                {
                    rebuilt.Remove(key);
                    rule[key] = value;
                }

                written++;
            }
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        // Newlines are written LF explicitly. The packs are LF in the repository, and letting this run
        // on Windows rewrite them as CRLF would mark every line of a 900-line file as changed the
        // first time anyone regenerates the thresholds.
        var json = root.ToJsonString(options).ReplaceLineEndings("\n") + "\n";
        File.WriteAllText(packPath, json);
        return written;
    }
}

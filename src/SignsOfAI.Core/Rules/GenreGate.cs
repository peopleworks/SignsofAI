using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Rules;

/// <summary>
/// Marks the findings of rules that are describing the genre rather than the machine.
///
/// Measuring the analyzer against ninety texts published before generative models existed produced a
/// number worth staring at: a median of <b>seven</b> flagged tells per human academic paper, 888
/// across the corpus, with only two of the ninety coming back clean. The score survived that — the
/// median was 6.8 out of 100 and nothing reached the recommended threshold — because the rules
/// involved carry weights of one and two. So the false-positive *rate* was never the problem.
///
/// The problem is the evidence panel, and it costs this project more than it would cost anyone else,
/// because showing the evidence instead of a percentage is the entire argument. A teacher who pastes
/// a colleague's paper and gets seven confident-looking tells learns not to believe the eighth, which
/// is the one that mattered.
///
/// <para><b>Why this marks rather than deletes.</b> Deleting was the first attempt and it was wrong in
/// three ways that are worth keeping written down. It made evidence vanish as a document grew, which
/// also handed anyone a way to bury a tell by padding. It cut the live rewriter off from words it
/// knows how to replace, for a writer whose goal is removing "utilize", not proving anything about it.
/// And it answered "our evidence panel is noisy" by hiding evidence, from a tool that exists to show
/// it. Marking keeps every finding visible and honest about what it is: present, and present at a rate
/// people write at.</para>
///
/// <para><b>What this does not fix.</b> The rate is measured per rule, and language models are tuned
/// away from repeating any single tell — the recognisable shape of machine prose is fifteen different
/// tells appearing once each, every one of them individually at a human rate. Marking rather than
/// deleting means that shape stays visible and countable, but nothing here scores it. Measuring
/// breadth rather than density is a separate question and it needs its own evidence, not a constant
/// chosen today.</para>
/// </summary>
public static class GenreGate
{
    /// <summary>
    /// <paramref name="findings"/> with the genre-rate ones flagged <see cref="Finding.AtHumanRate"/>.
    /// Findings whose rule carries no measured human rate are returned untouched.
    ///
    /// <paramref name="wordCount"/> must be the word count of the same text the findings came from,
    /// as counted by <c>StatisticsCalculator</c> — the thresholds are derived against that counter, so
    /// a different one silently rescales every comparison.
    /// </summary>
    public static IReadOnlyList<Finding> Apply(
        IReadOnlyList<Finding> findings, RulePack pack, int wordCount)
    {
        if (findings.Count == 0 || wordCount <= 0) return findings;

        var thresholds = pack.HumanRates;
        if (thresholds.Count == 0) return findings;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in findings)
            if (thresholds.ContainsKey(f.RuleId))
                counts[f.RuleId] = counts.GetValueOrDefault(f.RuleId) + 1;

        if (counts.Count == 0) return findings;

        var atHumanRate = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (ruleId, hits) in counts)
            if (hits / (double)wordCount * 1000.0 <= thresholds[ruleId])
                atHumanRate.Add(ruleId);

        return atHumanRate.Count == 0
            ? findings
            : [.. findings.Select(f => atHumanRate.Contains(f.RuleId) ? f with { AtHumanRate = true } : f)];
    }
}

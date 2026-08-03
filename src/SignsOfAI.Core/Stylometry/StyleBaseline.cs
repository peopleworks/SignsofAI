using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Rules;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Core.Stylometry;

/// <summary>
/// Compares a piece of writing against the same person's earlier work, using the frequencies of
/// function words — Burrows's Delta, rebuilt around a question it can actually answer.
///
/// **Why this exists.** Every tool in this category asks "does this look like a machine", and the
/// answer punishes anyone whose ordinary register is formal, which is most people writing in a second
/// language: detectors flag 61% of non-native English essays as AI-written. Asking "does this look
/// like the person who wrote the other things" inverts that, because a formal writer's own baseline is
/// already formal. It is the single strongest reply to the strongest objection to the whole category.
///
/// **Why it is measured this way.** Classical Delta asks which of several candidate authors a text
/// resembles, which forces a threshold that somebody has to invent. Here the yardstick belongs to the
/// writer: the report says how far the questioned text sits from their centre *and how far their own
/// pieces sit from it*, measured identically. Nobody has to accept our idea of "too far", because the
/// scale is the writer's own variation.
///
/// **Two things this deliberately will not do.** It will not return a verdict — there is no value
/// meaning "someone else wrote this", because style moves with the assignment, the genre, the deadline
/// and with a person simply improving. And it will not produce a number from thin evidence; below the
/// minimums it reports that it cannot say, because a distance computed from four hundred words is
/// noise wearing a decimal point, and a decimal point is exactly what makes people believe things.
///
/// The most useful outcome is the reassuring one. A submission that lands inside the writer's own
/// range is the result that settles a suspicion, and settling suspicions is most of what an integrity
/// process should be doing.
/// </summary>
public static class StyleBaseline
{
    /// <summary>Words per chunk. Short enough to get four out of three school essays, long enough that common function words still have countable rates.</summary>
    private const int ChunkWords = 350;

    /// <summary>A tail shorter than this joins the chunk before it rather than standing as a thin one.</summary>
    private const int MinTailWords = 200;

    /// <summary>
    /// Four, so that leaving one out still leaves three to compute a spread from. Three would let a
    /// standard deviation be drawn from two points, which is a number but not a measurement.
    /// </summary>
    private const int MinBaselineChunks = 4;

    private const int MinBaselineWords = 1_400;
    private const int MinQuestionedWords = 300;

    /// <summary>A function word has to actually appear to carry information about anybody's style.</summary>
    private const int MinOccurrences = 5;

    private const int MaxFeatures = 120;

    /// <summary>Past the writer's own widest gap by this much is still "the edge", not "beyond".</summary>
    private const double EdgeTolerance = 1.15;

    /// <summary>Samples this uneven make any comparison weak, and the report says so.</summary>
    private const double BroadRatio = 2.0;

    private static readonly RulePack NeutralPack = new() { Language = "*" };

    /// <param name="baseline">The writer's own earlier work. More and longer is better.</param>
    /// <param name="questioned">The piece being asked about.</param>
    /// <param name="language">"en" or "es" — decides which function words are used.</param>
    /// <param name="pack">Supplies the wording and, if it carries one, the function-word list.</param>
    public static BaselineReport Compare(
        IReadOnlyList<AuthorSample>? baseline,
        AuthorSample? questioned,
        string language = "en",
        RulePack? pack = null)
    {
        pack ??= NeutralPack;
        var words = FunctionWords(language, pack);

        if (questioned is null || baseline is null || baseline.Count == 0 || words.Count == 0)
            return Unavailable(pack, PackMessages.StyleNeedSamples, 0, MinBaselineChunks);

        // Normalized first, for the same reason the catalog is: published work attacks author
        // attribution with exactly the zero-width and lookalike characters the scanner removes, and a
        // baseline that can be poisoned is not a baseline.
        var questionedWords = WordsOf(questioned.Text);
        if (questionedWords.Count < MinQuestionedWords)
            return Unavailable(pack, PackMessages.StyleNeedQuestioned, questionedWords.Count, MinQuestionedWords);

        var chunks = new List<List<string>>();
        int baselineWordCount = 0;
        foreach (var sample in baseline)
        {
            var sampleWords = WordsOf(sample.Text);
            baselineWordCount += sampleWords.Count;
            chunks.AddRange(Chunk(sampleWords));
        }

        if (baselineWordCount < MinBaselineWords)
            return Unavailable(pack, PackMessages.StyleNeedBaseline, baselineWordCount, MinBaselineWords);
        if (chunks.Count < MinBaselineChunks)
            return Unavailable(pack, PackMessages.StyleNeedSamples, chunks.Count, MinBaselineChunks);

        // ---- features -------------------------------------------------------------------------
        var chunkRates = chunks.Select(c => Rates(c, words)).ToList();
        var questionedRates = Rates(questionedWords, words);

        var features = SelectFeatures(words, chunkRates, chunks);
        if (features.Count < 10)
        {
            // There is plenty of writing here; what is missing is this language's function words in
            // it. Almost always the wrong language was selected, and saying "not enough work" would
            // send someone off to gather more text that would not help either.
            return BaselineReport.NotAvailable(
                pack.Text(PackMessages.StyleNeedLanguage),
                pack.Text(PackMessages.StyleAdviceInsufficient));
        }

        // ---- the writer's own spread, leave-one-out -----------------------------------------------
        // Each of the writer's chunks is measured against the *other* chunks. Including a chunk in the
        // statistics it is then scored against would pull its own distance down, making the writer's
        // range look tighter than it is — and a tighter range makes the questioned text look further
        // out. That bias runs against the person being asked about, so it is not one to leave in.
        var within = new List<double>(chunks.Count);
        for (int i = 0; i < chunkRates.Count; i++)
        {
            var others = chunkRates.Where((_, j) => j != i).ToList();
            var stats = Standardise(features, others);
            within.Add(Distance(features, chunkRates[i], stats));
        }
        within.Sort();

        // ---- the questioned text, against all of them ----------------------------------------------
        var baselineStats = Standardise(features, chunkRates);
        double distance = Distance(features, questionedRates, baselineStats);

        // The per-word range the writer has actually shown, which needs no statistics to check.
        var observed = features.ToDictionary(
            f => f,
            f => (Low: chunkRates.Min(r => r[f]), High: chunkRates.Max(r => r[f])),
            StringComparer.Ordinal);
        int outside = features.Count(f =>
            questionedRates[f] < observed[f].Low || questionedRates[f] > observed[f].High);

        double max = within[^1];
        double median = within[within.Count / 2];
        bool broad = median > 0 && max / median > BroadRatio;

        // Placement is decided by the aggregate alone, on one rule anybody can restate: is this
        // further from the writer's centre than their own pieces are.
        //
        // The outside-the-range count deliberately does *not* feed it. On the handful of documents
        // measured so far a writer's own work put 0-3 words out of ~80 outside their range and a
        // plainly different voice put 14 of 93 — a real separation, and nowhere near enough evidence
        // to place a cut point on. Inventing one would be the exact move this product refuses
        // everywhere else, so the count is reported as a fact and a person reads it. Calibrating it
        // properly needs a corpus of known-authorship texts, which is its own piece of work.
        var placement = distance <= max ? BaselinePlacement.WithinRange
            : distance <= max * EdgeTolerance ? BaselinePlacement.AtTheEdge
            : BaselinePlacement.BeyondRange;

        var summaryKey = placement switch
        {
            BaselinePlacement.WithinRange => PackMessages.StyleSummaryWithin,
            BaselinePlacement.AtTheEdge => PackMessages.StyleSummaryEdge,
            _ => PackMessages.StyleSummaryBeyond,
        };
        var summary = pack.Text(summaryKey, Round(distance), Round(max));
        if (outside > 0) summary += " " + pack.Text(PackMessages.StyleNoteOutside, outside, features.Count);
        if (broad) summary += " " + pack.Text(PackMessages.StyleNoteBroad);

        return new BaselineReport
        {
            Placement = placement,
            Distance = Round(distance),
            WithinAuthorMax = Round(max),
            WithinAuthorMedian = Round(median),
            WithinAuthorDistances = [.. within.Select(Round)],
            Drivers = TopDrivers(features, questionedRates, baselineStats, observed),
            BaselineWordCount = baselineWordCount,
            QuestionedWordCount = questionedWords.Count,
            SampleCount = baseline.Count,
            FeatureCount = features.Count,
            WordsOutsideOwnRange = outside,
            BaselineIsBroad = broad,
            Summary = summary,
            Advice = pack.Text(PackMessages.StyleAdvice),
        };
    }

    // ---- measurement --------------------------------------------------------------------------------

    private sealed record FeatureStats(double Mean, double StdDev);

    /// <summary>
    /// Keeps the function words that the writer actually uses often enough to say something, ordered by
    /// how much they use them. A word appearing twice in fourteen hundred words carries noise, not
    /// style, and one they never use at all has no spread to measure against.
    /// </summary>
    private static List<string> SelectFeatures(
        IReadOnlyList<string> words, List<Dictionary<string, double>> chunkRates, List<List<string>> chunks)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var chunk in chunks)
            foreach (var word in chunk)
                totals[word] = totals.GetValueOrDefault(word) + 1;

        return [.. words
            .Where(w => totals.TryGetValue(w, out int n) && n >= MinOccurrences)
            .Where(w => StdDev(chunkRates.Select(r => r[w])) > 1e-9)
            .OrderByDescending(w => totals[w])
            .Take(MaxFeatures)];
    }

    private static Dictionary<string, FeatureStats> Standardise(
        List<string> features, List<Dictionary<string, double>> rates)
    {
        var stats = new Dictionary<string, FeatureStats>(features.Count, StringComparer.Ordinal);
        foreach (var feature in features)
        {
            var values = rates.Select(r => r[feature]).ToList();
            double mean = values.Average();
            double sd = StdDev(values);
            stats[feature] = new FeatureStats(mean, sd);
        }
        return stats;
    }

    /// <summary>Burrows's Delta: the mean absolute z-score across the features.</summary>
    private static double Distance(
        List<string> features, Dictionary<string, double> rates, Dictionary<string, FeatureStats> stats)
    {
        double total = 0;
        int counted = 0;
        foreach (var feature in features)
        {
            var (mean, sd) = stats[feature];
            if (sd < 1e-9) continue; // no spread in this slice: the feature says nothing here
            total += Math.Abs((rates[feature] - mean) / sd);
            counted++;
        }
        return counted == 0 ? 0 : total / counted;
    }

    private static List<StyleDriver> TopDrivers(
        List<string> features, Dictionary<string, double> questioned,
        Dictionary<string, FeatureStats> stats, Dictionary<string, (double Low, double High)> observed) =>
        [.. features
            .Where(f => stats[f].StdDev > 1e-9)
            .Select(f => new StyleDriver
            {
                Word = f,
                ZScore = Round((questioned[f] - stats[f].Mean) / stats[f].StdDev),
                QuestionedRate = Round(questioned[f] * 1000),
                BaselineRate = Round(stats[f].Mean * 1000),
                BaselineLowest = Round(observed[f].Low * 1000),
                BaselineHighest = Round(observed[f].High * 1000),
            })
            .OrderByDescending(d => Math.Abs(d.ZScore))
            .Take(8)];

    // ---- text ---------------------------------------------------------------------------------------

    /// <summary>Lower-cased words of the normalized text, so a substituted letter cannot shift a rate.</summary>
    private static List<string> WordsOf(string? text)
    {
        var normalized = TextNormalizer.Normalize(text ?? string.Empty).Text;
        return [.. new TextDocument(normalized).Words.Select(w => w.Text.ToLowerInvariant())];
    }

    private static List<List<string>> Chunk(List<string> words)
    {
        var chunks = new List<List<string>>();
        for (int i = 0; i < words.Count; i += ChunkWords)
            chunks.Add(words.GetRange(i, Math.Min(ChunkWords, words.Count - i)));

        // A thin tail would be measured on too few counts and would widen the writer's range for the
        // wrong reason, so it joins the chunk before it.
        if (chunks.Count > 1 && chunks[^1].Count < MinTailWords)
        {
            chunks[^2].AddRange(chunks[^1]);
            chunks.RemoveAt(chunks.Count - 1);
        }
        return chunks;
    }

    private static Dictionary<string, double> Rates(List<string> words, IReadOnlyList<string> vocabulary)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var word in words)
            counts[word] = counts.GetValueOrDefault(word) + 1;

        double total = Math.Max(1, words.Count);
        var rates = new Dictionary<string, double>(vocabulary.Count, StringComparer.Ordinal);
        foreach (var term in vocabulary)
            rates[term] = counts.GetValueOrDefault(term) / total;
        return rates;
    }

    /// <summary>
    /// The function words for a language. Taken from the rule pack, which makes them a JSON list
    /// anyone can extend with a pull request — adding a language here needs no compiler, which is the
    /// same bargain the detection rules already make.
    /// </summary>
    private static IReadOnlyList<string> FunctionWords(string language, RulePack pack)
    {
        if (pack.FunctionWords is { Length: > 0 } supplied) return supplied;

        var builtIn = RulePackLoader.Load(string.IsNullOrWhiteSpace(language) ? "en" : language);
        return builtIn.FunctionWords ?? [];
    }

    private static double StdDev(IEnumerable<double> values)
    {
        var list = values as IList<double> ?? [.. values];
        if (list.Count < 2) return 0;
        double mean = list.Average();
        return Math.Sqrt(list.Sum(v => (v - mean) * (v - mean)) / (list.Count - 1));
    }

    private static double Round(double value) => Math.Round(value, 3);

    private static BaselineReport Unavailable(RulePack pack, string key, int have, int need) =>
        BaselineReport.NotAvailable(
            pack.Text(key, have, need),
            pack.Text(PackMessages.StyleAdviceInsufficient));
}

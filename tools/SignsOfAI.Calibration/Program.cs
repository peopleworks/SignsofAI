using System.Reflection;
using System.Text.Json;
using SignsOfAI.Calibration;
using SignsOfAI.Core;
using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Calibration;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Text;

// ── signsofai-calibrate ───────────────────────────────────────────────────────
// Measures the analyzer against writing that is known to be human, and publishes what it finds.
//
// This exists because "what is your accuracy?" is the first question every teacher asks and no tool
// in this category answers it. The answer here is deliberately narrower than the question: not an
// accuracy figure, which would need a collection of machine-written text that ages badly and
// flatters whoever assembled it, but a false-positive rate, which needs only human writing and
// measures the harm this category actually does.

try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* redirected */ }

var argv = args.ToList();
if (argv.Count == 0 || argv[0] is "-h" or "--help" or "help")
{
    Help();
    return 0;
}

string manifestPath = "Docs/Calibration/corpus.json";
string textsDir = "";
string outPath = "Docs/CALIBRATION.md";
bool recordHashes = false;
bool allowMissing = false;
string source = "";
string fetchLanguage = "en";
int count = 40, fromYear = 2018, toYear = 2020;
string packsDir = "src/SignsOfAI.Core/Rules/Packs";
string pairsPath = "Docs/Paraphrase/pairs.json";
string humanDir = "", rewrittenDir = "";
int perStratum = 8, targetWords = 400;
string paraphrasedBy = "", instructionPath = "";

for (int i = 1; i < argv.Count; i++)
{
    switch (argv[i])
    {
        case "--manifest": manifestPath = Next(); break;
        case "--texts": textsDir = Next(); break;
        case "--out": outPath = Next(); break;
        case "--record-hashes": recordHashes = true; break;
        case "--allow-missing": allowMissing = true; break;
        case "--packs": packsDir = Next(); break;
        case "--source": source = Next(); break;
        case "--lang": fetchLanguage = Next(); break;
        case "--count": count = int.Parse(Next()); break;
        case "--from-year": fromYear = int.Parse(Next()); break;
        case "--to-year": toYear = int.Parse(Next()); break;
        case "--pairs": pairsPath = Next(); break;
        case "--human": humanDir = Next(); break;
        case "--rewritten": rewrittenDir = Next(); break;
        case "--per-stratum": perStratum = int.Parse(Next()); break;
        case "--words": targetWords = int.Parse(Next()); break;
        case "--paraphrased-by": paraphrasedBy = Next(); break;
        case "--instruction": instructionPath = Next(); break;
        default:
            Console.Error.WriteLine($"Unknown option '{argv[i]}'.");
            return 2;
    }
    string Next() => ++i < argv.Count ? argv[i] : throw new ArgumentException($"Missing value for {argv[i - 1]}");
}

if (argv[0] is not ("run" or "fetch" or "thresholds" or "excerpt" or "paraphrase"))
{
    Console.Error.WriteLine($"Unknown command '{argv[0]}'. Run --help.");
    return 2;
}

// ── `fetch` ──────────────────────────────────────────────────────────────────
// Adds texts that are human by virtue of their date rather than by anybody's judgement, and appends
// them to the manifest. Nothing fetched is redistributed: the texts stay on this machine and the
// repository carries only the index.
if (argv[0] == "fetch")
{
    if (string.IsNullOrWhiteSpace(textsDir))
        textsDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, "texts");

    var existing = File.Exists(manifestPath)
        ? CorpusManifest.Load(manifestPath)
        : new CorpusManifest { Id = "signsofai-human-baseline", TargetFalsePositiveRate = 0.05 };

    List<CorpusEntry> fetched;
    switch (source)
    {
        case "plos":
            Console.WriteLine($"Fetching {count} PLOS research articles, {fromYear}-{toYear}…");
            fetched = await Fetch.PlosAsync(count, textsDir, fromYear, toYear);
            break;
        case "wikipedia":
            Console.WriteLine($"Fetching {count} pre-2022 {fetchLanguage}.wikipedia revisions…");
            fetched = await Fetch.WikipediaAsync(fetchLanguage, count, textsDir);
            break;
        case "pelic":
            Console.WriteLine("Selecting learner essays from PELIC (Pittsburgh, 2006–2012)…");
            fetched = await Fetch.PelicAsync(textsDir);
            break;
        default:
            Console.Error.WriteLine("--source must be 'plos', 'wikipedia' or 'pelic'.");
            return 2;
    }

    var known = existing.Texts.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
    existing.Texts.AddRange(fetched.Where(f => known.Add(f.Id)));
    existing.Texts.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    existing.Save(manifestPath);

    Console.WriteLine();
    Console.WriteLine($"  added {fetched.Count}, manifest now {existing.Texts.Count} texts");
    Console.WriteLine($"  {manifestPath}");
    return 0;
}

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"Manifest not found: {manifestPath}");
    return 2;
}

var manifest = CorpusManifest.Load(manifestPath);
if (string.IsNullOrWhiteSpace(textsDir))
    textsDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, "texts");

// ── `excerpt` ────────────────────────────────────────────────────────────────
// Builds the human half of the paraphrase study: a stratified sample of the corpus, cut to
// equal-length passages of continuous prose. Nothing is rewritten here — that is done by a model,
// outside this tool, and recorded in the manifest this verb writes.
if (argv[0] == "excerpt")
{
    if (string.IsNullOrWhiteSpace(humanDir))
        humanDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pairsPath))!, "human");

    Directory.CreateDirectory(humanDir);

    var picked = Paraphrase.Sample(manifest, perStratum);
    var pairs = new PairManifest
    {
        Id = "signsofai-paraphrase-effect",
        CorpusId = manifest.Id,
        TargetWords = targetWords,
    };

    int skipped = 0;
    foreach (var entry in picked)
    {
        var file = Path.Combine(textsDir, entry.File);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"  missing  {entry.Id}  ({entry.File})");
            skipped++;
            continue;
        }

        var excerpt = Paraphrase.Excerpt(File.ReadAllText(file), targetWords);
        var words = Paraphrase.CountWords(excerpt);

        // A passage too short to have a distribution of sentence lengths cannot answer the question
        // this study asks, and padding it out of a different part of the article would make the two
        // halves of the pair no longer the same passage.
        if (words < Paraphrase.MinimumWords)
        {
            Console.Error.WriteLine($"  too short  {entry.Id}  ({words} words)");
            skipped++;
            continue;
        }

        File.WriteAllText(Path.Combine(humanDir, $"{entry.Id}.txt"),
            excerpt.ReplaceLineEndings("\n") + "\n");

        pairs.Pairs.Add(new PairEntry
        {
            Id = entry.Id,
            Language = entry.Language,
            Stratum = entry.Stratum,
            Year = entry.Year,
            Source = entry.Url ?? entry.Doi ?? "",
            HumanSha256 = CorpusManifest.HashText(excerpt.ReplaceLineEndings("\n") + "\n"),
            HumanWords = words,
        });
    }

    pairs.Pairs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    pairs.Save(pairsPath);

    Console.WriteLine();
    Console.WriteLine($"  {pairs.Pairs.Count} excerpts written" + (skipped > 0 ? $", {skipped} skipped" : ""));
    foreach (var group in pairs.Pairs.GroupBy(p => p.Stratum).OrderBy(g => g.Key, StringComparer.Ordinal))
        Console.WriteLine($"      {group.Key,-28} {group.Count(),2} texts, {group.Sum(p => p.HumanWords),6:N0} words");
    Console.WriteLine();
    Console.WriteLine($"  passages  {humanDir}");
    Console.WriteLine($"  manifest  {pairsPath}");
    Console.WriteLine();
    Console.WriteLine("  Next: rewrite each passage into the 'rewritten' folder under the same name,");
    Console.WriteLine("  then run 'paraphrase --paraphrased-by <model> --instruction <file>'.");
    return 0;
}

// ── `paraphrase` ─────────────────────────────────────────────────────────────
// Measures both halves of every pair and writes the study up. The corpus manifest is not consulted
// here: the pairs carry their own provenance, and the human halves have already been hashed.
if (argv[0] == "paraphrase")
{
    var pairsDir = Path.GetDirectoryName(Path.GetFullPath(pairsPath))!;
    if (string.IsNullOrWhiteSpace(humanDir)) humanDir = Path.Combine(pairsDir, "human");
    if (string.IsNullOrWhiteSpace(rewrittenDir)) rewrittenDir = Path.Combine(pairsDir, "rewritten");

    if (!File.Exists(pairsPath))
    {
        Console.Error.WriteLine($"No pair manifest at {pairsPath}. Run 'excerpt' first.");
        return 2;
    }

    var pairManifest = PairManifest.Load(pairsPath);
    if (!string.IsNullOrWhiteSpace(paraphrasedBy)) pairManifest.ParaphrasedBy = paraphrasedBy;
    if (!string.IsNullOrWhiteSpace(instructionPath))
        pairManifest.Instruction = File.ReadAllText(instructionPath).ReplaceLineEndings("\n");

    if (string.IsNullOrWhiteSpace(pairManifest.ParaphrasedBy))
    {
        // Refused rather than defaulted. A study of what a model does to prose, whose manifest does
        // not name the model, produces a number nobody can reproduce or date.
        Console.Error.WriteLine("--paraphrased-by is required: name the model that did the rewriting.");
        return 2;
    }

    var pairAnalyzer = new AiWritingAnalyzer();
    var measurements = new List<PairMeasurement>();
    int incomplete = 0, changed = 0;

    // Where each control window starts, as a fraction of the document's prose. Three positions
    // rather than one because the study's own pairs are cut from the opening, and the opening of a
    // research article is its abstract while the opening of an encyclopedia entry is its lead —
    // the most summary-shaped prose either genre contains. A length effect measured only there has
    // a genre effect inside it, which is precisely the mistake the first version of this page made.
    (string Label, double Position)[] WindowPositions =
    [
        ("opening", 0.0),
        ("middle", 0.5),
        ("late", 0.75),
    ];

    foreach (var pair in pairManifest.Pairs)
    {
        var humanFile = Path.Combine(humanDir, $"{pair.Id}.txt");
        var rewrittenFile = Path.Combine(rewrittenDir, $"{pair.Id}.txt");

        if (!File.Exists(humanFile) || !File.Exists(rewrittenFile))
        {
            Console.Error.WriteLine($"  incomplete  {pair.Id}");
            incomplete++;
            continue;
        }

        var humanText = File.ReadAllText(humanFile);
        var rewrittenText = File.ReadAllText(rewrittenFile);

        // The human half is hashed in the manifest, so a passage that drifted after the rewriting was
        // done would silently break the pairing. The rewritten half is hashed on this run, since it
        // does not exist until somebody produces it.
        var humanHash = CorpusManifest.HashText(humanText);
        if (!string.Equals(pair.HumanSha256, humanHash, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"  CHANGED  {pair.Id}  manifest {pair.HumanSha256[..12]}…, file {humanHash[..12]}…");
            changed++;
            continue;
        }

        // The rewritten half is verified once it has a hash, and only assigned when it does not.
        // The first version reassigned it on every run, which meant an edited rewrite was absorbed
        // and re-hashed in silence — the manifest recording whatever was last on disk rather than
        // what the study measured.
        var rewrittenHash = CorpusManifest.HashText(rewrittenText);
        if (pair.ParaphraseSha256 is { Length: > 0 } expectedRewrite)
        {
            if (!string.Equals(expectedRewrite, rewrittenHash, StringComparison.OrdinalIgnoreCase))
            {
                if (!recordHashes)
                {
                    Console.Error.WriteLine($"  CHANGED  {pair.Id}  rewrite differs from the manifest " +
                                            $"({expectedRewrite[..12]}… vs {rewrittenHash[..12]}…)");
                    changed++;
                    continue;
                }

                Console.WriteLine($"  re-recorded  {pair.Id}");
                pair.ParaphraseSha256 = rewrittenHash;
            }
        }
        else
        {
            pair.ParaphraseSha256 = rewrittenHash;
        }

        pair.ParaphraseWords = Paraphrase.CountWords(rewrittenText);

        var humanResult = pairAnalyzer.Analyze(humanText, pair.Language);
        var rewrittenResult = pairAnalyzer.Analyze(rewrittenText, pair.Language);

        // How much of the original actually survived. Measured rather than attested, because the
        // first version of this study attested it by hand and the hand was wrong.
        var (longestRun, shareRetained) = Fidelity.Measure(humanText, rewrittenText);

        // The document the excerpt was cut from, and windows taken across it. Without these a reader
        // cannot tell which of three things moved a score: the scissors, where the scissors fell, or
        // the model.
        AnalysisResult? fullResult = null, proseResult = null;
        var windows = new List<WindowMeasurement>();

        if (manifest.Texts.FirstOrDefault(t => t.Id == pair.Id) is { } sourceEntry)
        {
            var sourcePath = Path.Combine(textsDir, sourceEntry.File);
            if (File.Exists(sourcePath))
            {
                var sourceText = File.ReadAllText(sourcePath);

                // The third arm gets the same hash check the human half gets. It was reading these
                // files and analyzing them with no verification at all, which left one arm of a
                // published study resting on whatever happened to be in the directory.
                var sourceHash = CorpusManifest.HashText(sourceText);
                if (sourceEntry.Sha256 is { Length: > 0 } expectedSource &&
                    !string.Equals(expectedSource, sourceHash, StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"  CHANGED  {pair.Id}  source text differs from the corpus manifest");
                    changed++;
                    continue;
                }

                fullResult = pairAnalyzer.Analyze(sourceText, pair.Language);

                var prose = Paraphrase.ProseOnly(sourceText);
                if (Paraphrase.CountWords(prose) >= Paraphrase.MinimumWords)
                    proseResult = pairAnalyzer.Analyze(prose, pair.Language);

                foreach (var (label, position) in WindowPositions)
                {
                    var window = Paraphrase.WindowAt(sourceText, pairManifest.TargetWords, position);
                    if (Paraphrase.CountWords(window) < Paraphrase.MinimumWords) continue;

                    var windowResult = pairAnalyzer.Analyze(window, pair.Language);
                    windows.Add(new WindowMeasurement
                    {
                        Position = label,
                        Score = windowResult.OverallScore,
                        Burstiness = windowResult.Statistics.Burstiness,
                        Words = windowResult.Statistics.WordCount,
                    });
                }
            }
        }

        var humanScan = ArtifactScanner.Scan(humanText);
        var rewrittenScan = ArtifactScanner.Scan(rewrittenText);

        measurements.Add(new PairMeasurement
        {
            FullScore = fullResult?.OverallScore,
            FullBurstiness = fullResult?.Statistics.Burstiness,
            FullWords = fullResult?.Statistics.WordCount ?? 0,
            ProseOnlyScore = proseResult?.OverallScore,
            Windows = windows,
            LongestRun = longestRun,
            ShareRetained = shareRetained,
            ArtifactCodePoints = [.. humanScan.Occurrences.Concat(rewrittenScan.Occurrences)
                .Select(o => o.CodePoint).Distinct()],
            Id = pair.Id,
            Language = pair.Language,
            Stratum = pair.Stratum,
            HumanScore = humanResult.OverallScore,
            RewrittenScore = rewrittenResult.OverallScore,
            HumanBurstiness = humanResult.Statistics.Burstiness,
            RewrittenBurstiness = rewrittenResult.Statistics.Burstiness,
            HumanWords = humanResult.Statistics.WordCount,
            RewrittenWords = rewrittenResult.Statistics.WordCount,
            HumanRuleIds = [.. humanResult.Findings.Where(f => !f.AtHumanRate).Select(f => f.RuleId)],
            RewrittenRuleIds = [.. rewrittenResult.Findings.Where(f => !f.AtHumanRate).Select(f => f.RuleId)],
            HumanArtifacts = humanScan.Count,
            RewrittenArtifacts = rewrittenScan.Count,
        });
    }

    if (changed > 0)
    {
        Console.Error.WriteLine($"\n{changed} passage(s) no longer match the manifest. Re-run 'excerpt'.");
        return 1;
    }

    // Stamped only when it has never been stamped, or when the run is explicitly re-recording. The
    // first version set it on every measurement, so re-analyzing in October would have made the
    // manifest claim the rewriting happened in October — falsifying the one date this design says
    // must never be vague.
    if (string.IsNullOrWhiteSpace(pairManifest.ParaphrasedOn) || recordHashes)
        pairManifest.ParaphrasedOn = DateTime.UtcNow.ToString("yyyy-MM-dd");

    pairManifest.Save(pairsPath);

    var pairVersion = typeof(AiWritingAnalyzer).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? "unknown";

    // The boundary is read from the engine's own published snapshot rather than restated here, so the
    // study cannot quote a threshold the product has since moved away from.
    var boundary = PublishedCalibration.Current?.RecommendedThreshold ?? 25;

    var paraphraseReport = ParaphraseReport.Render(
        measurements, pairManifest, boundary, $"SignsOfAI.Core {pairVersion}",
        DateTime.UtcNow.ToString("yyyy-MM-dd"));

    var paraphraseOut = outPath == "Docs/CALIBRATION.md" ? "Docs/PARAPHRASE.md" : outPath;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(paraphraseOut))!);
    File.WriteAllText(paraphraseOut, paraphraseReport.ReplaceLineEndings("\n"));

    int wasFlagged = measurements.Count(m => m.HumanScore >= boundary);
    int nowFlagged = measurements.Count(m => m.RewrittenScore >= boundary);

    Console.WriteLine();
    Console.WriteLine($"  {measurements.Count} pairs measured" + (incomplete > 0 ? $", {incomplete} incomplete" : ""));
    Console.WriteLine($"  pair fingerprint    {pairManifest.Fingerprint()}");
    Console.WriteLine($"  flagged at {boundary:0}/100    {wasFlagged}  →  {nowFlagged}  of {measurements.Count}");
    Console.WriteLine($"  median score        {Median([.. measurements.Select(m => m.HumanScore)]):0.0}" +
                      $"  →  {Median([.. measurements.Select(m => m.RewrittenScore)]):0.0}");
    Console.WriteLine($"  median burstiness   {Median([.. measurements.Select(m => m.HumanBurstiness)]):0.00}" +
                      $"  →  {Median([.. measurements.Select(m => m.RewrittenBurstiness)]):0.00}");
    Console.WriteLine($"  written to          {paraphraseOut}");
    Console.WriteLine();
    return 0;

    static double Median(List<double> values)
    {
        values.Sort();
        return values.Count == 0 ? 0
            : values.Count % 2 == 1 ? values[values.Count / 2]
            : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2;
    }
}

var analyzer = new AiWritingAnalyzer();
var samples = new List<CalibrationSample>();
int missing = 0, mismatched = 0;

foreach (var entry in manifest.Texts)
{
    var file = Path.Combine(textsDir, entry.File);
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"  missing  {entry.Id}  ({entry.File})");
        missing++;
        continue;
    }

    var text = File.ReadAllText(file);
    var hash = CorpusManifest.HashText(text);

    if (recordHashes)
    {
        entry.Sha256 = hash;
    }
    else if (entry.Sha256 is { Length: > 0 } expected && !string.Equals(expected, hash, StringComparison.OrdinalIgnoreCase))
    {
        // A silently changed text would move a published number without anybody noticing, which is
        // the failure this hash exists to prevent.
        Console.Error.WriteLine($"  CHANGED  {entry.Id}  manifest {expected[..12]}…, file {hash[..12]}…");
        mismatched++;
        continue;
    }

    var result = analyzer.Analyze(text, entry.Language);
    samples.Add(new CalibrationSample
    {
        Id = entry.Id,
        Language = entry.Language,
        Stratum = entry.Stratum,
        Score = result.OverallScore,
        WordCount = result.Statistics.WordCount,
        RuleIds = [.. result.Findings.Where(f => !f.AtHumanRate).Select(f => f.RuleId)],
        MatchedRuleIds = [.. result.Findings.Select(f => f.RuleId)],
    });
}

if (recordHashes)
{
    manifest.Save(manifestPath);
    Console.WriteLine($"Recorded hashes for {samples.Count} texts in {manifestPath}.");
}

if (mismatched > 0)
{
    Console.Error.WriteLine($"\n{mismatched} text(s) no longer match the manifest. " +
                            "Re-run with --record-hashes if the change was intended.");
    return 1;
}

// A manifest entry with no file is not a smaller corpus, it is a different one: the page would
// carry the manifest's fingerprint over numbers measured on a subset. A reviewer showed this
// returning 0 with an empty texts folder and a report reading "the corpus is empty".
if (missing > 0 && !allowMissing)
{
    Console.Error.WriteLine($"\n{missing} text(s) named in the manifest are not in {textsDir}. " +
                            "Fetch them, or pass --allow-missing to measure the subset on purpose.");
    return 1;
}

// ── `thresholds` ─────────────────────────────────────────────────────────────
// Derives each rule's human usage rate and writes it into the packs, so the numbers the analyzer
// runs on can be regenerated by anyone holding the corpus rather than taken on trust.
if (argv[0] == "thresholds")
{
    var derived = Thresholds.Derive(samples);
    foreach (var (language, rates) in derived.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        var pack = Path.Combine(packsDir, $"rules.{language}.json");
        if (!File.Exists(pack))
        {
            Console.Error.WriteLine($"  no pack for '{language}' at {pack}");
            continue;
        }

        var written = Thresholds.WriteInto(pack, rates);
        Console.WriteLine($"  {language}: {written} rules given a measured rate  →  {pack}");
        foreach (var (id, rate) in rates.OrderByDescending(r => r.Value))
            Console.WriteLine($"      {id,-28} {rate,5:0.00} per 1,000 words");
    }

    var (before, after, cleanBefore, cleanAfter) = Thresholds.LeaveOneOut(samples);
    Console.WriteLine();
    Console.WriteLine("  Held out of its own thresholds, each text keeps:");
    Console.WriteLine($"      findings per text   {before:0.0}  →  {after:0.0}  ({(after - before) / before:P0})");
    Console.WriteLine($"      texts with none     {cleanBefore}  →  {cleanAfter}  of {samples.Count}");
    Console.WriteLine();
    return 0;
}

var calibration = Calibrator.Compute(
    samples, manifest.Id, manifest.Fingerprint(), manifest.TargetFalsePositiveRate);

var version = typeof(AiWritingAnalyzer).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
    ?? "unknown";

// The date is stamped from the run rather than baked in, and it is the only non-deterministic thing
// on the page — everything else is a function of the corpus and the engine.
var report = Report.Render(calibration, manifest, $"SignsOfAI.Core {version}",
    DateTime.UtcNow.ToString("yyyy-MM-dd"));

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, report);

// The same numbers, in the form the engine can carry. A report that accuses somebody has to print
// its own error rate on the same page, and reading it back out of the Markdown — or restating it
// from memory in C# — is how a page ends up quoting a threshold three versions old.
var overall = calibration.Overall;
var atThreshold = overall.ThresholdForTarget is { } chosen
    ? overall.Thresholds.FirstOrDefault(t => Math.Abs(t.Threshold - chosen) < 0.001)
    : null;

var published = new PublishedCalibration
{
    CorpusId = manifest.Id,
    Texts = samples.Count,
    MeasuredOn = DateTime.UtcNow.ToString("yyyy-MM-dd"),
    Engine = version,
    RecommendedThreshold = overall.ThresholdForTarget,
    FlaggedAtThreshold = atThreshold?.Flagged ?? 0,
    RateLow = atThreshold?.RateLow ?? 0,
    RateHigh = atThreshold?.RateHigh ?? 1,

    // The lengths the boundary was actually fitted on. The engine withholds its verdict below the
    // shorter of the two rather than extrapolating a bound onto a population nobody measured — the
    // same rule the language condition follows, applied to the other dimension. See issue #59.
    ShortestWords = overall.ShortestWords,
    LongestWords = overall.LongestWords,
    NoisiestRules = [.. calibration.RuleFalsePositives.Take(8)
        .Select(r => new PublishedRuleRate { RuleId = r.RuleId, TextShare = r.TextShare })],

    // Per language, because the report about a Spanish essay must not quote an English-heavy
    // aggregate. The best bound a group can support is the tightest upper interval it reaches at any
    // threshold; when that never gets inside the target, the group supports no threshold and the
    // report has to say so instead of borrowing the overall one.
    Languages = [.. calibration.ByLanguage.Select(g => new PublishedLanguage
    {
        Language = g.Name,
        Texts = g.Count,
        RecommendedThreshold = g.ThresholdForTarget,
        BestBound = g.Thresholds.Count == 0 ? 1 : g.Thresholds.Min(t => t.RateHigh),
        RateHighAtThreshold = g.ThresholdForTarget is { } own
            ? g.Thresholds.FirstOrDefault(t => Math.Abs(t.Threshold - own) < 0.001)?.RateHigh
            : null,
    })],
};

// Anchored to the manifest rather than the working directory. Run from anywhere but the repository
// root, the old version silently created a second copy under the current folder while
// Docs/CALIBRATION.md updated correctly — leaving the real embedded snapshot stale, and the build
// happily embedding last month's threshold under this build's name.
var repoRoot = Path.GetFullPath(Path.Combine(
    Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, "..", ".."));
var embedPath = Path.Combine(repoRoot, "src", "SignsOfAI.Core", "Calibration", "published-calibration.json");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(embedPath))!);
var publishedJson = JsonSerializer.Serialize(
    published, PublishedCalibrationJsonContext.Default.PublishedCalibration);
File.WriteAllText(embedPath, publishedJson.ReplaceLineEndings("\n") + "\n");

Console.WriteLine();
Console.WriteLine($"  {samples.Count} texts measured" + (missing > 0 ? $", {missing} missing" : ""));
Console.WriteLine($"  corpus fingerprint  {calibration.CorpusHash}");
Console.WriteLine($"  median score        {calibration.Overall.MedianScore:0.0}");
Console.WriteLine($"  90th percentile     {calibration.Overall.NinetiethScore:0.0}");
Console.WriteLine($"  lengths measured    {calibration.Overall.ShortestWords:N0}–{calibration.Overall.LongestWords:N0} words" +
                  $"  (median {calibration.Overall.MedianWords:N0}) — below the shortest, no verdict");
Console.WriteLine(calibration.Overall.ThresholdForTarget is { } t
    ? $"  threshold for {calibration.TargetFalsePositiveRate:P0}   {t:0}/100"
    : $"  threshold for {calibration.TargetFalsePositiveRate:P0}   not supported by this corpus yet");
Console.WriteLine($"  written to          {outPath}");
Console.WriteLine();

return 0;

static void Help() => Console.WriteLine(
    """
    signsofai-calibrate — measure this analyzer against writing known to be human.

    USAGE
      dotnet run --project tools/SignsOfAI.Calibration -- run [options]

      dotnet run --project tools/SignsOfAI.Calibration -- fetch --source plos --count 40
      dotnet run --project tools/SignsOfAI.Calibration -- fetch --source wikipedia --lang es --count 30
      dotnet run --project tools/SignsOfAI.Calibration -- fetch --source pelic

      dotnet run --project tools/SignsOfAI.Calibration -- excerpt --per-stratum 8 --words 400
      dotnet run --project tools/SignsOfAI.Calibration -- paraphrase --paraphrased-by "<model>"

    OPTIONS
      --manifest <file>   Corpus manifest (default: Docs/Calibration/corpus.json)
      --texts <dir>       Where the texts live (default: <manifest dir>/texts)
      --out <file>        Where to write the report (default: Docs/CALIBRATION.md)
      --record-hashes     Record each text's SHA-256 into the manifest. Use once, when
                          assembling or deliberately updating the corpus.
      --allow-missing     run only: measure even if some manifest texts are absent. Off by
                          default — a partial corpus under the full manifest's fingerprint is
                          a number nobody can reproduce.
      --source <name>     fetch only: 'plos', 'wikipedia' or 'pelic' (learner essays; --count is
                          ignored — the selection is a fixed rule, so everyone gets the same texts)
      --lang <code>       fetch only: language for wikipedia (default: en)
      --count <n>         fetch only: how many texts (default: 40)
      --from-year/--to-year   fetch only: publication window for plos (default 2018-2020)
      --pairs <file>      Pair manifest for the paraphrase study (default: Docs/Paraphrase/pairs.json)
      --human <dir>       Passages as their authors wrote them (default: <pairs dir>/human)
      --rewritten <dir>   The same passages after a model rewrote them (default: <pairs dir>/rewritten)
      --per-stratum <n>   excerpt only: passages to take from each group (default: 8)
      --words <n>         excerpt only: length to cut each passage to (default: 400)
      --paraphrased-by <name>   paraphrase only: the model that did the rewriting. Required —
                          a study of what a model does to prose that cannot name the model
                          has produced a number nobody can reproduce or date.
      --instruction <file>      paraphrase only: the instruction the rewriter was given,
                          stored verbatim in the manifest because it is the treatment.
      --record-hashes     paraphrase: accept a rewritten half that differs from the manifest
                          and re-stamp the rewriting date. Use only when the rewriting has
                          genuinely been redone — every other run refuses to overwrite them,
                          so a published study cannot quietly acquire different material.

    WHY
      "What is your accuracy?" is the first question a teacher asks and the one no tool in
      this category answers. This does not answer it either — it answers a narrower question
      that can be answered honestly: how often does this flag writing that a machine did not
      produce. Accuracy needs a collection of machine-written text, which is a sample of
      whichever models were around that month. A false-positive rate needs only human
      writing, and it measures the harm this category actually causes.

      The texts are not in the repository; the manifest is. See Docs/Calibration/README.md.

      `excerpt` and `paraphrase` answer a second question with the same corpus: what does a
      language model rewriting a passage do to it. Each unit is one passage measured as its
      author wrote it and again after a model rewrote it, so no collection of machine-written
      text is needed — the baseline is the passage itself. See Docs/Paraphrase/README.md.
    """);

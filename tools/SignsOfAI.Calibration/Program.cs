using System.Reflection;
using System.Text.Json;
using SignsOfAI.Calibration;
using SignsOfAI.Core;
using SignsOfAI.Core.Calibration;
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
string source = "";
string fetchLanguage = "en";
int count = 40, fromYear = 2018, toYear = 2020;
string packsDir = "src/SignsOfAI.Core/Rules/Packs";

for (int i = 1; i < argv.Count; i++)
{
    switch (argv[i])
    {
        case "--manifest": manifestPath = Next(); break;
        case "--texts": textsDir = Next(); break;
        case "--out": outPath = Next(); break;
        case "--record-hashes": recordHashes = true; break;
        case "--packs": packsDir = Next(); break;
        case "--source": source = Next(); break;
        case "--lang": fetchLanguage = Next(); break;
        case "--count": count = int.Parse(Next()); break;
        case "--from-year": fromYear = int.Parse(Next()); break;
        case "--to-year": toYear = int.Parse(Next()); break;
        default:
            Console.Error.WriteLine($"Unknown option '{argv[i]}'.");
            return 2;
    }
    string Next() => ++i < argv.Count ? argv[i] : throw new ArgumentException($"Missing value for {argv[i - 1]}");
}

if (argv[0] is not ("run" or "fetch" or "thresholds"))
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
        default:
            Console.Error.WriteLine("--source must be 'plos' or 'wikipedia'.");
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
    NoisiestRules = [.. calibration.RuleFalsePositives.Take(8)
        .Select(r => new PublishedRuleRate { RuleId = r.RuleId, TextShare = r.TextShare })],
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

    OPTIONS
      --manifest <file>   Corpus manifest (default: Docs/Calibration/corpus.json)
      --texts <dir>       Where the texts live (default: <manifest dir>/texts)
      --out <file>        Where to write the report (default: Docs/CALIBRATION.md)
      --record-hashes     Record each text's SHA-256 into the manifest. Use once, when
                          assembling or deliberately updating the corpus.
      --source <name>     fetch only: 'plos' or 'wikipedia'
      --lang <code>       fetch only: language for wikipedia (default: en)
      --count <n>         fetch only: how many texts (default: 40)
      --from-year/--to-year   fetch only: publication window for plos (default 2018-2020)

    WHY
      "What is your accuracy?" is the first question a teacher asks and the one no tool in
      this category answers. This does not answer it either — it answers a narrower question
      that can be answered honestly: how often does this flag writing that a machine did not
      produce. Accuracy needs a collection of machine-written text, which is a sample of
      whichever models were around that month. A false-positive rate needs only human
      writing, and it measures the harm this category actually causes.

      The texts are not in the repository; the manifest is. See Docs/Calibration/README.md.
    """);

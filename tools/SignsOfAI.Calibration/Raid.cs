using System.Text.Json;
using System.Text.Json.Serialization;
using SignsOfAI.Core;
using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Text;

namespace SignsOfAI.Calibration;

/// <summary>
/// Scores a batch of texts for an outside benchmark, RAID (Dugan et al., ACL 2024), and nothing else.
///
/// The rest of this tool measures the half of the question that needs only human writing: how often
/// the engine is wrong about a person. This verb serves the other half, how much machine-written text
/// the score separates, and it deliberately does no evaluating of its own. The benchmark's own code
/// chooses the threshold and counts the hits, so the number cannot be shaped by whoever wrote the
/// scorer. All this does is run the shipped engine, unchanged, and write down what it said.
/// </summary>
public static class Raid
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed record Input(string Id, string Text);

    public sealed record Output
    {
        public required string Id { get; init; }

        /// <summary>The 0–100 the reader sees, before the length floor. RAID gets this divided by 100.</summary>
        public required double Score { get; init; }

        public required int Words { get; init; }

        /// <summary>Findings above the human rate: the ones that move the score.</summary>
        public required int Signals { get; init; }

        /// <summary>Which rules those were, so a result can be explained rule by rule.</summary>
        public required IReadOnlyList<string> Rules { get; init; }

        /// <summary>What the product would have guessed, recorded because the scoring itself is told "en".</summary>
        public required string DetectedLanguage { get; init; }

        /// <summary>
        /// The character scan, reported apart from the score and never folded into it. Three levels so
        /// the results can be read at each one rather than at whichever flatters the scanner.
        /// </summary>
        public required int Artifacts { get; init; }
        public required int StrongArtifacts { get; init; }
        public required string ArtifactPattern { get; init; }
    }

    /// <summary>Reads JSON lines of <c>{id, text}</c> and writes one scored line per input, in order.</summary>
    public static int Run(string inPath, string outPath, string language = "en", int threads = -1)
    {
        if (!File.Exists(inPath))
        {
            Console.Error.WriteLine($"Input not found: {inPath}");
            return 2;
        }

        var inputs = new List<Input>();
        foreach (var line in File.ReadLines(inPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var input = JsonSerializer.Deserialize<Input>(line, Json)
                        ?? throw new InvalidDataException($"Unreadable line in {inPath}");
            inputs.Add(input);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            if (!ids.Add(input.Id))
            {
                // RAID joins predictions to its rows by id; a duplicate would be scored twice and
                // silently count double in whichever cell it falls into.
                Console.Error.WriteLine($"Duplicate id in input: {input.Id}");
                return 2;
            }
        }

        Console.WriteLine($"Scoring {inputs.Count:N0} texts as '{language}'…");
        var started = DateTime.UtcNow;

        var outputs = new Output[inputs.Count];
        using var analyzer = new ThreadLocal<AiWritingAnalyzer>(() => new AiWritingAnalyzer());
        // One analyzer per thread. --threads 1 exists to prove the parallel run gives the same answers.
        var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
        Parallel.For(0, inputs.Count, options, i => outputs[i] = Score(analyzer.Value!, inputs[i], language));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        using (var writer = new StreamWriter(outPath, append: false, new System.Text.UTF8Encoding(false)))
        {
            foreach (var output in outputs)
                writer.WriteLine(JsonSerializer.Serialize(output, Json));
        }

        var elapsed = DateTime.UtcNow - started;
        Console.WriteLine($"  {inputs.Count:N0} scored in {elapsed.TotalSeconds:N1} s → {outPath}");
        return 0;
    }

    public static Output Score(AiWritingAnalyzer analyzer, Input input, string language)
    {
        var text = input.Text ?? string.Empty;
        var result = analyzer.Analyze(text, language);

        // The scan the product shows is of the original text, not the cleaned copy the rules read,
        // so the attack is seen exactly as a reader's file would carry it.
        var scan = result.Artifacts;

        return new Output
        {
            Id = input.Id,
            Score = result.OverallScore,
            Words = result.Statistics.WordCount,
            Signals = result.Signals.Count,
            Rules = [.. result.Signals.Select(f => f.RuleId).Distinct().Order(StringComparer.Ordinal)],
            DetectedLanguage = LanguageDetector.Detect(text),
            Artifacts = scan.Count,
            StrongArtifacts = scan.StrongCount,
            ArtifactPattern = scan.Pattern.ToString(),
        };
    }
}

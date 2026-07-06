using System.Text.Json;
using SignsOfAI.Core;
using SignsOfAI.Core.Documents;
using SignsOfAI.Core.Model;

// ── signsofai: lint prose for the signs of AI writing ────────────────────────
const string Version = "0.1.0";

// Emit UTF-8 so accents, · separators and glyphs render on Windows consoles too.
try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* redirected / unsupported */ }

var argList = args.ToList();

if (argList.Count == 0 || argList[0] is "-h" or "--help" or "help")
{
    PrintHelp();
    return 0;
}
if (argList[0] is "--version" or "-v")
{
    Console.WriteLine(Version);
    return 0;
}
if (argList[0] != "check")
{
    Console.Error.WriteLine($"Unknown command '{argList[0]}'. Run 'signsofai --help'.");
    return 2;
}

// ── parse `check <path> [options]` ───────────────────────────────────────────
var positionals = new List<string>();
string language = "auto";
bool json = false, noColor = false;
double? maxScore = null;
int top = 10;

for (int i = 1; i < argList.Count; i++)
{
    var a = argList[i];
    switch (a)
    {
        case "--lang": language = Next(); break;
        case "--json": json = true; break;
        case "--no-color": noColor = true; break;
        case "--max-score": maxScore = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
        case "--top": top = int.Parse(Next()); break;
        default:
            if (a.StartsWith('-')) { Console.Error.WriteLine($"Unknown option '{a}'."); return 2; }
            positionals.Add(a); break;
    }
    string Next() => ++i < argList.Count ? argList[i] : throw new ArgumentException($"Missing value for {a}");
}

if (positionals.Count == 0)
{
    Console.Error.WriteLine("Usage: signsofai check <path> [--lang auto|en|es] [--json] [--max-score N] [--top N]");
    return 2;
}

var path = positionals[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"File not found: {path}");
    return 2;
}

// ── read (supports .docx) & analyze ──────────────────────────────────────────
string text;
try
{
    text = path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
        ? await DocxTextExtractor.ExtractTextAsync(File.OpenRead(path))
        : await File.ReadAllTextAsync(path);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Could not read '{path}': {ex.Message}");
    return 2;
}

var result = new AiWritingAnalyzer().Analyze(text, language);

if (json)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        file = path,
        language = result.Language,
        score = result.OverallScore,
        verdict = result.Verdict,
        statistics = result.Statistics,
        categories = result.CategoryScores,
        findings = result.Findings.Select(f => new
        {
            f.RuleId, category = f.Category.ToString(), severity = f.Severity.ToString(),
            f.MatchedText, f.Message, f.Suggestion, f.Evidence
        }),
    }, new JsonSerializerOptions { WriteIndented = true }));
}
else
{
    PrintReport(path, result, top, useColor: !noColor && !Console.IsOutputRedirected
        && Environment.GetEnvironmentVariable("NO_COLOR") is null);
}

// ── CI gate ──────────────────────────────────────────────────────────────────
if (maxScore is { } max && result.OverallScore > max)
{
    Console.Error.WriteLine($"✗ Score {result.OverallScore:0} exceeds --max-score {max:0}.");
    return 1;
}
return 0;

// ── helpers ──────────────────────────────────────────────────────────────────
static void PrintReport(string path, AnalysisResult r, int top, bool useColor)
{
    string Col(string s, int code) => useColor ? $"[{code}m{s}[0m" : s;
    string Bold(string s) => useColor ? $"[1m{s}[0m" : s;

    int scoreColor = r.OverallScore switch { >= 70 => 31, >= 45 => 33, >= 20 => 33, _ => 32 };
    Console.WriteLine();
    Console.WriteLine(Bold($"  ✍  Signs of AI Writing — {Path.GetFileName(path)}"));
    Console.WriteLine($"     {Col($"{r.OverallScore:0}/100", scoreColor)}  {Bold(r.Verdict)}   " +
                      $"({r.Findings.Count} signal{(r.Findings.Count == 1 ? "" : "s")}, {(r.Language == "es" ? "Español" : "English")})");
    Console.WriteLine($"     words {r.Statistics.WordCount} · sentences {r.Statistics.SentenceCount} · " +
                      $"burstiness {r.Statistics.Burstiness:0.00} · lexical diversity {r.Statistics.LexicalDiversity:0.00}");

    var cats = r.CategoryScores.Where(c => c.FindingCount > 0).ToList();
    if (cats.Count > 0)
        Console.WriteLine("     " + string.Join("  ", cats.Select(c => $"{c.Category} {c.FindingCount}")));

    Console.WriteLine();
    var shown = r.Findings.Take(top).ToList();
    foreach (var f in shown)
    {
        int sev = f.Severity switch { Severity.High => 31, Severity.Medium => 33, Severity.Low => 36, _ => 90 };
        var head = $"  {Col("●", sev)} [{f.Category}] " + (string.IsNullOrEmpty(f.MatchedText) ? "" : Bold(f.MatchedText));
        Console.WriteLine(head.TrimEnd());
        Console.WriteLine($"      {f.Message}");
        Console.WriteLine(Col($"      → {f.Suggestion}", 90));
    }
    if (r.Findings.Count > shown.Count)
        Console.WriteLine(Col($"  … and {r.Findings.Count - shown.Count} more (use --top {r.Findings.Count}).", 90));
    if (r.Findings.Count == 0)
        Console.WriteLine(Col("  ✓ No strong AI tells found — reads mostly human.", 32));
    Console.WriteLine();
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        signsofai — detect the signs of AI writing (English & Spanish) and recommend fixes.

        USAGE
          signsofai check <path> [options]

        OPTIONS
          --lang <auto|en|es>   Language of the text (default: auto-detect)
          --json                Emit a JSON report instead of the pretty report
          --max-score <N>       Exit with code 1 if the overall score exceeds N (for CI gating)
          --top <N>             Show at most N findings in the pretty report (default: 10)
          --no-color            Disable ANSI colors
          -h, --help            Show this help
          --version             Show the version

        EXAMPLES
          signsofai check README.md
          signsofai check article.docx --lang en
          signsofai check post.md --max-score 40      # fail CI if it reads too much like AI
          signsofai check post.md --json > report.json
        """);
}

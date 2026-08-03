using System.Text.Json;
using SignsOfAI.Core;
using SignsOfAI.Core.Artifacts;
using SignsOfAI.Core.Citations;
using SignsOfAI.Core.Documents;
using SignsOfAI.Core.Model;
using SignsOfAI.Core.Rules;

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
var ruleFiles = new List<string>();
string language = "auto";
bool json = false, noColor = false, failOnArtifacts = false;
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
        case "--rules": ruleFiles.Add(Next()); break;
        case "--max-score": maxScore = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture); break;
        case "--fail-on-artifacts": failOnArtifacts = true; break;
        case "--top": top = int.Parse(Next()); break;
        default:
            if (a.StartsWith('-')) { Console.Error.WriteLine($"Unknown option '{a}'."); return 2; }
            positionals.Add(a); break;
    }
    string Next() => ++i < argList.Count ? argList[i] : throw new ArgumentException($"Missing value for {a}");
}

if (positionals.Count == 0)
{
    Console.Error.WriteLine("Usage: signsofai check <path> [--lang auto|en|es] [--json] [--max-score N] [--fail-on-artifacts] [--top N]");
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

// Load any custom catalogs (--rules file.json, repeatable).
var extraPacks = new List<RulePack>();
foreach (var rf in ruleFiles)
{
    if (!File.Exists(rf)) { Console.Error.WriteLine($"Rule-pack not found: {rf}"); return 2; }
    try { extraPacks.Add(RulePack.FromJson(await File.ReadAllTextAsync(rf))); }
    catch (Exception ex) { Console.Error.WriteLine($"Invalid rule-pack '{rf}': {ex.Message}"); return 2; }
}

var result = new AiWritingAnalyzer().Analyze(text, language, extraPacks);

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
        // Kept in its own object rather than folded in with the findings: these are characters at
        // offsets, not judgements about prose, and a consumer should not have to tell them apart.
        artifacts = new
        {
            pattern = result.Artifacts.Pattern.ToString(),
            count = result.Artifacts.Count,
            strongCount = result.Artifacts.StrongCount,
            sectionsAffected = result.Artifacts.SectionsAffected,
            sectionCount = result.Artifacts.SectionCount,
            summary = result.Artifacts.Summary,
            advice = result.Artifacts.Advice,
            groups = result.Artifacts.Groups.Select(g => new
            {
                kind = g.Kind.ToString(), g.CodePoint, g.CharacterName, g.LooksLike, g.Count, g.IsStrong
            }),
            occurrences = result.Artifacts.Occurrences.Select(o => new
            {
                kind = o.Kind.ToString(), o.CodePoint, o.CharacterName, o.LooksLike, o.Word,
                o.Line, o.Column, offset = o.Span.Start, o.IsStrong, o.Message
            }),
        },
        // Also its own object: what the document says about its own sources is a statement about the
        // file, not a reading of the prose, and a consumer should not have to separate them.
        citations = new
        {
            style = result.Citations.Style.ToString(),
            hasReferenceList = result.Citations.HasReferenceList,
            referenceCount = result.Citations.References.Count,
            citationCount = result.Citations.Citations.Count,
            contradictionCount = result.Citations.ContradictionCount,
            summary = result.Citations.Summary,
            advice = result.Citations.Advice,
            issues = result.Citations.Issues.Select(i => new
            {
                kind = i.Kind.ToString(), i.Line, i.Subject, i.Message, i.IsContradiction
            }),
        },
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

// Gates on the strong artifacts only. Non-breaking spaces arrive with any copy-paste and failing a
// build over them would train everyone to pass the flag and stop reading.
if (failOnArtifacts && result.Artifacts.StrongCount > 0)
{
    Console.Error.WriteLine(
        $"✗ {result.Artifacts.StrongCount} character artifact(s) that typing does not produce.");
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

    PrintArtifacts(r.Artifacts, Col, Bold);
    PrintCitations(r.Citations, Col, Bold);

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

/// <summary>
/// Prints what the document says about its own sources, and nothing at all when it has none.
/// Contradictions are listed first and marked; the untidy ones follow, uncoloured, because listing a
/// source you never cited is a housekeeping note and should not read like an accusation.
/// </summary>
static void PrintCitations(CitationReport report, Func<string, int, string> Col, Func<string, string> Bold)
{
    if (!report.Any) return;

    Console.WriteLine();
    Console.WriteLine("     " + Bold("Sources") + "  " +
                      Col(report.ContradictionCount > 0
                          ? $"{report.ContradictionCount} contradiction(s)"
                          : report.HasReferenceList ? "text and bibliography agree" : "no reference list",
                          report.ContradictionCount > 0 ? 33 : 90));
    Console.WriteLine(Col($"     {report.Summary}", 90));

    foreach (var issue in report.Issues.OrderByDescending(i => i.IsContradiction).ThenBy(i => i.Line))
    {
        var mark = issue.IsContradiction ? Col("!", 33) : Col("·", 90);
        var line = $"       {mark} line {issue.Line}: {issue.Message}";
        Console.WriteLine(issue.IsContradiction ? line : Col(line, 90));
    }

    if (report.Issues.Count > 0)
        Console.WriteLine(Col($"     {report.Advice}", 90));
}

/// <summary>
/// Prints the character-artifact report, and nothing at all when there is none — which is nearly
/// every file. Kept above the findings and visibly apart from them: the findings are a reading of
/// the prose, this is a list of characters at offsets that anyone can go and verify.
/// </summary>
static void PrintArtifacts(ArtifactReport report, Func<string, int, string> Col, Func<string, string> Bold)
{
    if (!report.Any) return;

    bool systematic = report.Pattern == ArtifactPattern.Systematic;
    Console.WriteLine();
    Console.WriteLine("     " + Bold("Character artifacts") + "  " +
                      Col(systematic ? "spread through the document" : "present, not spread",
                          systematic ? 33 : 90));
    Console.WriteLine(Col($"     {report.Summary}", 90));

    foreach (var g in report.Groups)
    {
        var looks = g.LooksLike is null ? "" : $"  looks like \"{g.LooksLike}\"";
        Console.WriteLine($"       {g.CodePoint,-8} {g.CharacterName}{looks}  ×{g.Count}");
    }

    var first = report.Occurrences.Where(o => o.IsStrong).Take(6).ToList();
    if (first.Count > 0)
        Console.WriteLine(Col("       at " +
            string.Join(" · ", first.Select(o => $"line {o.Line}, col {o.Column}")) +
            (report.StrongCount > first.Count ? $" · +{report.StrongCount - first.Count} more" : ""), 90));

    Console.WriteLine(Col($"     {report.Advice}", 90));
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
          --rules <file.json>   Add a custom catalog (rule-pack). Repeatable.
          --json                Emit a JSON report instead of the pretty report
          --max-score <N>       Exit with code 1 if the overall score exceeds N (for CI gating)
          --fail-on-artifacts   Exit with code 1 if the text holds characters typing cannot produce
                                (invisible characters, letters impersonating Latin ones)
          --top <N>             Show at most N findings in the pretty report (default: 10)
          --no-color            Disable ANSI colors
          -h, --help            Show this help
          --version             Show the version

        EXAMPLES
          signsofai check README.md
          signsofai check article.docx --lang en
          signsofai check post.md --max-score 40      # fail CI if it reads too much like AI
          signsofai check post.md --json > report.json
          signsofai check essay.docx --fail-on-artifacts   # reject text a rewriting tool has been through
        """);
}

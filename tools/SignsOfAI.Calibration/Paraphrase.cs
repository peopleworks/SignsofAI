using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignsOfAI.Calibration;

/// <summary>
/// One text measured twice: as its author wrote it, and after a language model rewrote it.
///
/// The pair is the whole design. Every other way of asking "what does a machine paraphrase do to a
/// text" needs a collection of machine-written text to compare against, which is the thing
/// <c>Docs/Calibration/README.md</c> argues at length against assembling. A pair needs none: both
/// halves are the same passage, by the same author, about the same subject, at the same length, so
/// anything that moves between them moved because of the rewrite and nothing else. The baseline is
/// not estimated, it is the text itself.
/// </summary>
public sealed class PairEntry
{
    /// <summary>The corpus entry this came from, so the pair can be traced back to a DOI.</summary>
    public required string Id { get; set; }

    public required string Language { get; set; }

    public required string Stratum { get; set; }

    /// <summary>Year of the source publication — the reason the human half is known to be human.</summary>
    public required int Year { get; set; }

    public required string Source { get; set; }

    /// <summary>SHA-256 of the excerpt as its author wrote it.</summary>
    public required string HumanSha256 { get; set; }

    /// <summary>
    /// SHA-256 of the rewritten excerpt, or null until one exists. Null is the normal state after
    /// <c>excerpt</c> and before the rewriting has been done, and the measuring verb refuses to run
    /// on a manifest that still contains any.
    /// </summary>
    public string? ParaphraseSha256 { get; set; }

    public int HumanWords { get; set; }

    public int ParaphraseWords { get; set; }
}

/// <summary>
/// The index of the study, and the one place its expiry date is written down.
///
/// The human corpus does not age: a 2019 paper will still have been written in 2019 in ten years.
/// This half does age, and pretending otherwise would repeat the mistake the calibration page exists
/// to avoid. So the model that did the rewriting and the date it ran are recorded as data rather
/// than as prose, the report prints them in its first paragraph, and a reader can see at a glance
/// whether the number in front of them was measured on something current.
/// </summary>
public sealed class PairManifest
{
    public required string Id { get; set; }

    /// <summary>Which corpus the human halves were drawn from.</summary>
    public required string CorpusId { get; set; }

    /// <summary>
    /// The model that produced the rewrites, named exactly. "An LLM" is not a method; a different
    /// model gives different prose and would move every number on the page.
    /// </summary>
    public string? ParaphrasedBy { get; set; }

    /// <summary>UTC date the rewriting was done.</summary>
    public string? ParaphrasedOn { get; set; }

    /// <summary>
    /// The instruction the rewriter was given, stored verbatim. It is the experimental treatment: a
    /// study of "what a paraphrase does" whose paraphrase instruction is lost has measured something
    /// nobody can name afterwards.
    /// </summary>
    public string? Instruction { get; set; }

    /// <summary>Words each excerpt was cut to, before rewriting.</summary>
    public int TargetWords { get; set; } = 400;

    public List<PairEntry> Pairs { get; set; } = [];

    public static PairManifest Load(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), PairJson.Default.PairManifest)
        ?? throw new InvalidOperationException($"'{path}' deserialized to null.");

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(this, PairJson.Default.PairManifest);
        File.WriteAllText(path, json.ReplaceLineEndings("\n") + "\n");
    }

    /// <summary>
    /// A fingerprint over both halves of every pair. The calibration manifest has one for the same
    /// reason: a published figure and the material it was measured on must not drift apart quietly.
    /// </summary>
    public string Fingerprint()
    {
        var canonical = string.Join('\n', Pairs
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => $"{p.Id}\t{p.HumanSha256}\t{p.ParaphraseSha256}"));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }
}

/// <summary>
/// Builds the human half of the study: a stratified sample of the calibration corpus, cut to
/// equal-length excerpts that a model can be asked to rewrite.
/// </summary>
public static class Paraphrase
{
    /// <summary>
    /// Below roughly this many words the statistical signals are measuring the sample rather than
    /// the writing — few sentences means few sentence lengths, and a coefficient of variation over
    /// eight of them is noise with a decimal point. It is the same limitation Anthropic states about
    /// its own watermark on short passages, and it applies here for the same reason.
    /// </summary>
    public const int MinimumWords = 250;

    /// <summary>
    /// Picks <paramref name="perStratum"/> texts from each group, the same ones on every run.
    ///
    /// Deterministic by hashing the identity rather than by seeding a random number generator: a seed
    /// has to be remembered and passed around correctly forever, whereas a hash of the id gives the
    /// same sample to anybody holding the same corpus, with nothing to remember. Re-running after
    /// adding texts changes the sample only where the corpus changed.
    /// </summary>
    public static List<CorpusEntry> Sample(CorpusManifest corpus, int perStratum)
    {
        return [.. corpus.Texts
            .GroupBy(t => t.Stratum, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g
                .OrderBy(t => Convert.ToHexStringLower(
                    SHA256.HashData(Encoding.UTF8.GetBytes($"{corpus.Id}\t{t.Id}"))), StringComparer.Ordinal)
                .Take(perStratum))];
    }

    /// <summary>
    /// Cuts a passage of continuous prose to about <paramref name="targetWords"/> words, ending on a
    /// sentence boundary.
    ///
    /// Two things are being kept out. Reference lists and figure captions are prose-shaped but nobody
    /// composes them, so a rewrite of them measures citation formatting rather than writing. And the
    /// cut lands on a full stop because a truncated final sentence is a short sentence, which would
    /// push burstiness up in the human half by an artefact of the scissors — in precisely the
    /// direction that would flatter the result this study is looking for.
    /// </summary>
    public static string Excerpt(string text, int targetWords)
    {
        var kept = new StringBuilder();
        int words = 0;

        foreach (var paragraph in text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var p = paragraph.Trim();
            if (p.Length == 0 || !IsProse(p)) continue;

            kept.Append(p).Append("\n\n");
            words += CountWords(p);
            if (words >= targetWords) break;
        }

        return TrimToSentence(kept.ToString().TrimEnd(), targetWords);
    }

    /// <summary>
    /// Whether a paragraph is somebody's prose rather than apparatus.
    ///
    /// Deliberately blunt. The cost of wrongly dropping a good paragraph is that the excerpt starts
    /// one paragraph later; the cost of keeping a reference list is a pair whose two halves differ in
    /// ways that have nothing to do with writing. The errors are not symmetric, so this errs toward
    /// dropping.
    /// </summary>
    private static bool IsProse(string paragraph)
    {
        if (CountWords(paragraph) < 25) return false;

        // A bibliography entry is mostly names, years and identifiers; running prose is mostly not.
        double digits = paragraph.Count(char.IsDigit) / (double)paragraph.Length;
        if (digits > 0.08) return false;

        if (paragraph.Contains("doi:", StringComparison.OrdinalIgnoreCase) ||
            paragraph.Contains("http", StringComparison.OrdinalIgnoreCase)) return false;

        // Prose has sentences. A caption or a heading run-on has one full stop or none.
        int stops = paragraph.Count(c => c is '.' or '?' or '!');
        return stops >= 2;
    }

    /// <summary>
    /// Trims to the last sentence that fits. Abbreviations will occasionally fool this and end an
    /// excerpt mid-thought; that costs a slightly odd-looking passage and nothing measurable, since
    /// both halves of the pair are built from the same cut.
    /// </summary>
    private static string TrimToSentence(string text, int targetWords)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= targetWords) return text;

        // Walk back from the target to the nearest sentence end, so the excerpt is never longer than
        // asked for and never ends in the middle of a clause.
        int taken = 0, cut = text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]) && (i == 0 || !char.IsWhiteSpace(text[i - 1]))) taken++;
            if (taken >= targetWords) { cut = i; break; }
        }

        var window = text[..cut];
        int lastStop = window.LastIndexOfAny(['.', '?', '!']);
        return lastStop > 0 ? window[..(lastStop + 1)] : window;
    }

    public static int CountWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Where a window of <paramref name="targetWords"/> words starts, for a document cut at
    /// <paramref name="position"/> of the way through its prose.
    ///
    /// The study's own pairs are cut from the opening, which is what a naive implementation does and
    /// which turned out to matter: for a research article the opening is abstract and introduction,
    /// for an encyclopedia entry it is the lead, and both are the most summary-shaped prose their
    /// genre contains. A length effect measured only there is a length effect with a genre effect
    /// inside it. Positions are given rather than random so a run is reproducible.
    /// </summary>
    public static string WindowAt(string text, int targetWords, double position)
    {
        var paragraphs = text.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0 && IsProse(p))
            .ToList();

        if (paragraphs.Count == 0) return "";

        var kept = new StringBuilder();
        int words = 0;

        foreach (var paragraph in paragraphs.Skip((int)(paragraphs.Count * position)))
        {
            kept.Append(paragraph).Append("\n\n");
            words += CountWords(paragraph);
            if (words >= targetWords) break;
        }

        return TrimToSentence(kept.ToString().TrimEnd(), targetWords);
    }

    /// <summary>
    /// The whole document with its apparatus removed — the same <see cref="IsProse"/> filter the
    /// excerpts pass through, and no length cut.
    ///
    /// This exists because without it the three arms are not comparable: the excerpts have their
    /// figure captions and supporting-information boilerplate stripped and the whole documents do
    /// not, so a difference between them could be composition rather than length. Measuring this
    /// arm is what turns "the same writing" from a claim into something checked.
    /// </summary>
    public static string ProseOnly(string text) =>
        string.Join("\n\n", text.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0 && IsProse(p)));
}

/// <summary>
/// How much of the original survived the rewriting, measured rather than attested.
///
/// The first version of this study asserted its own compliance in prose and the assertion was wrong:
/// the method file recorded one deliberate breach of the instruction's "no run of eight or more
/// consecutive words" when the passages on disk contained many. That is the sort of claim a project
/// which machine-checks everything else it publishes has no business making by hand.
/// </summary>
public static class Fidelity
{
    /// <summary>The instruction's limit: no run of this many consecutive words may survive.</summary>
    public const int MaximumRun = 8;

    /// <summary>
    /// Words, lowercased, punctuation discarded.
    ///
    /// The permissive reading on purpose. A stricter tokenizer — case-sensitive, punctuation
    /// attached — finds fewer breaches, and the failure being corrected here is under-reporting
    /// them. Where two defensible measures disagree, the one that reports more of your own
    /// deviations is the one to publish.
    /// </summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) current.Append(char.ToLowerInvariant(ch));
            else if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
        }

        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens;
    }

    /// <summary>
    /// The longest run of consecutive words from <paramref name="original"/> that appears verbatim in
    /// <paramref name="rewritten"/>, and what share of the rewrite sits inside runs of at least
    /// <see cref="MaximumRun"/> words.
    /// </summary>
    public static (int LongestRun, double ShareRetained) Measure(string original, string rewritten)
    {
        var a = Tokenize(original);
        var b = Tokenize(rewritten);
        if (a.Count == 0 || b.Count == 0) return (0, 0);

        // Longest common substring by the usual rolling table, kept to one row because these are
        // four-hundred-word passages and the full table is never needed.
        var previous = new int[b.Count + 1];
        var current = new int[b.Count + 1];
        int longest = 0;
        var covered = new bool[b.Count];

        for (int i = 1; i <= a.Count; i++)
        {
            for (int j = 1; j <= b.Count; j++)
            {
                current[j] = a[i - 1] == b[j - 1] ? previous[j - 1] + 1 : 0;
                if (current[j] > longest) longest = current[j];

                // Mark every token of a qualifying run, so overlapping runs are counted once.
                if (current[j] >= MaximumRun)
                    for (int k = j - current[j]; k < j; k++) covered[k] = true;
            }

            (previous, current) = (current, previous);
            Array.Clear(current);
        }

        return (longest, covered.Count(c => c) / (double)b.Count);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PairManifest))]
public partial class PairJson : JsonSerializerContext;

using System.Globalization;
using System.Text;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Originality;

/// <summary>One document submitted for an originality comparison.</summary>
public sealed record OriginalityInput(string Id, string Title, string Text);

/// <summary>
/// A contiguous run of words that two documents share, anchored in each by a character span so the UI
/// can highlight the very same passage in both. <see cref="WordLength"/> is the run length in words.
/// </summary>
public sealed record SharedPassage(TextSpan SpanA, TextSpan SpanB, int WordLength);

/// <summary>
/// Result of comparing one pair of documents. The headline <see cref="Overlap"/> is <b>passage coverage</b>:
/// the fraction of the more-copied document's words that appear, verbatim (case- and accent-insensitive),
/// inside a shared passage. That number equals exactly what the UI highlights — the evidence <i>is</i> the score.
/// <see cref="Jaccard"/> and <see cref="Containment"/> are secondary set-similarity stats.
/// </summary>
public sealed record PairComparison(
    int IndexA, int IndexB,
    string TitleA, string TitleB,
    double CoverageA, double CoverageB,
    double Jaccard, double Containment,
    int SharedShingleCount, int LongestRunWords, int SharedWords,
    IReadOnlyList<SharedPassage> Passages)
{
    /// <summary>Headline overlap 0..1 — the more-covered side (what a reviewer cares about most).</summary>
    public double Overlap => Math.Max(CoverageA, CoverageB);
}

/// <summary>Full pairwise report over the submitted documents, pairs sorted most-overlapping first.</summary>
public sealed record OriginalityReport(
    int DocumentCount, int ShingleSize,
    IReadOnlyList<string> Titles,
    IReadOnlyList<int> WordCounts,
    IReadOnlyList<PairComparison> Pairs);

/// <summary>
/// A privacy-first, <b>client-side</b> originality / copy checker. It compares documents against
/// <i>each other</i> (a cohort of submissions, a draft against its sources) — it is <b>not</b> a
/// whole-internet index like Turnitin. The method is honest and deterministic:
/// <list type="number">
///   <item>normalize text (lower-case, strip accents, ignore punctuation) so trivial edits don't hide copies;</item>
///   <item>build overlapping word <i>k</i>-shingles and compare the shingle sets;</item>
///   <item>reconstruct the actual shared passages via greedy longest-match tiling, verifying real token
///         equality (never trusting a hash collision) so every reported passage is genuinely shared.</item>
/// </list>
/// We surface the matching passages as evidence and let a human judge — we never accuse.
/// Everything runs in the browser; the documents never leave the device.
/// </summary>
public sealed class OriginalityChecker
{
    /// <summary>Words per shingle. 5 is the classic near-duplicate default: long enough that ordinary
    /// phrases don't collide, short enough to catch lightly-edited copies.</summary>
    public int ShingleSize { get; }

    /// <summary>Shared runs shorter than this (in words) are ignored as coincidental common phrasing.</summary>
    public int MinPassageWords { get; }

    public OriginalityChecker(int shingleSize = 5, int minPassageWords = 8)
    {
        ShingleSize = Math.Max(2, shingleSize);
        MinPassageWords = Math.Max(ShingleSize, minPassageWords);
    }

    public OriginalityReport Check(IReadOnlyList<OriginalityInput> docs)
    {
        var models = new DocModel[docs.Count];
        for (int i = 0; i < docs.Count; i++) models[i] = Build(docs[i].Text);

        var pairs = new List<PairComparison>();
        for (int i = 0; i < docs.Count; i++)
            for (int j = i + 1; j < docs.Count; j++)
                pairs.Add(Compare(i, j, docs[i].Title, docs[j].Title, models[i], docs[i].Text, models[j], docs[j].Text));

        // Most-overlapping first; break ties by raw shared volume so identical scores rank sensibly.
        pairs.Sort((x, y) =>
        {
            int c = y.Overlap.CompareTo(x.Overlap);
            return c != 0 ? c : y.SharedWords.CompareTo(x.SharedWords);
        });

        return new OriginalityReport(
            docs.Count, ShingleSize,
            docs.Select(d => d.Title).ToArray(),
            models.Select(m => m.Tokens.Count).ToArray(),
            pairs);
    }

    // ── comparison ───────────────────────────────────────────────────────────
    private PairComparison Compare(int i, int j, string titleA, string titleB, DocModel a, string textA, DocModel b, string textB)
    {
        // Set-similarity over distinct shingles.
        var (small, big) = a.ShingleSet.Count <= b.ShingleSet.Count ? (a.ShingleSet, b.ShingleSet) : (b.ShingleSet, a.ShingleSet);
        int inter = 0;
        foreach (var h in small) if (big.Contains(h)) inter++;
        int union = a.ShingleSet.Count + b.ShingleSet.Count - inter;
        double jaccard = union == 0 ? 0 : (double)inter / union;
        int minSet = Math.Min(a.ShingleSet.Count, b.ShingleSet.Count);
        double containment = minSet == 0 ? 0 : (double)inter / minSet;

        // Reconstruct the actual shared passages (and per-side coverage) via greedy longest-match tiling.
        var coveredA = new bool[a.Tokens.Count];
        var coveredB = new bool[b.Tokens.Count];
        var passages = FindPassages(a, b, coveredA, coveredB);

        int longest = 0, shared = 0;
        foreach (var p in passages) { if (p.WordLength > longest) longest = p.WordLength; shared += p.WordLength; }
        double covA = a.Tokens.Count == 0 ? 0 : (double)CountTrue(coveredA) / a.Tokens.Count;
        double covB = b.Tokens.Count == 0 ? 0 : (double)CountTrue(coveredB) / b.Tokens.Count;
        int sharedWords = Math.Max(CountTrue(coveredA), CountTrue(coveredB));

        return new PairComparison(i, j, titleA, titleB, covA, covB, jaccard, containment, inter, longest, sharedWords, passages);
    }

    private List<SharedPassage> FindPassages(DocModel a, DocModel b, bool[] coveredA, bool[] coveredB)
    {
        var passages = new List<SharedPassage>();
        int k = ShingleSize;
        var ta = a.Tokens; var tb = b.Tokens;
        int nA = a.Shingles.Length;
        int idx = 0;
        while (idx < nA)
        {
            if (!b.Index.TryGetValue(a.Shingles[idx], out var bStarts)) { idx++; continue; }

            // Among every place B carries this shingle, take the longest run that is *actually* token-equal.
            int bestLen = 0, bestB = -1;
            foreach (var bs in bStarts)
            {
                if (!TokensEqual(ta, idx, tb, bs, k)) continue; // hash-collision guard
                int len = k;
                while (idx + len < ta.Count && bs + len < tb.Count && ta[idx + len].Norm == tb[bs + len].Norm) len++;
                if (len > bestLen) { bestLen = len; bestB = bs; }
            }

            if (bestLen == 0) { idx++; continue; } // only a hash collision here
            if (bestLen >= MinPassageWords)
            {
                passages.Add(new SharedPassage(SpanOf(ta, idx, bestLen), SpanOf(tb, bestB, bestLen), bestLen));
                for (int t = 0; t < bestLen; t++) { coveredA[idx + t] = true; coveredB[bestB + t] = true; }
            }
            idx += bestLen - k + 1; // skip the shingles fully inside this run
        }
        return passages;
    }

    // ── document model ───────────────────────────────────────────────────────
    private sealed class DocModel
    {
        public required List<Tok> Tokens;
        public required long[] Shingles;                 // hash per starting token index
        public required HashSet<long> ShingleSet;        // distinct shingles, for set similarity
        public required Dictionary<long, List<int>> Index; // shingle hash → its start indices (for tiling)
    }

    private DocModel Build(string text)
    {
        var toks = Tokenize(text);
        int k = ShingleSize;
        int m = Math.Max(0, toks.Count - k + 1);
        var sh = new long[m];
        var index = new Dictionary<long, List<int>>(m);
        for (int j = 0; j < m; j++)
        {
            long h = HashShingle(toks, j, k);
            sh[j] = h;
            if (!index.TryGetValue(h, out var list)) index[h] = list = new List<int>(1);
            list.Add(j);
        }
        return new DocModel { Tokens = toks, Shingles = sh, ShingleSet = new HashSet<long>(sh), Index = index };
    }

    // ── tokenization (accent/case-folded, but original spans preserved for highlighting) ──
    private readonly record struct Tok(string Norm, int Start, int Len)
    {
        public int End => Start + Len;
    }

    private static List<Tok> Tokenize(string text)
    {
        var toks = new List<Tok>();
        int i = 0, n = text.Length;
        while (i < n)
        {
            while (i < n && !char.IsLetterOrDigit(text[i])) i++;
            if (i >= n) break;
            int start = i;
            while (i < n && char.IsLetterOrDigit(text[i])) i++;
            var norm = Fold(text.AsSpan(start, i - start));
            if (norm.Length > 0) toks.Add(new Tok(norm, start, i - start));
        }
        return toks;
    }

    /// <summary>Lower-case and strip diacritics so "Canción"/"cancion" match — a common evasion.</summary>
    private static string Fold(ReadOnlySpan<char> s)
    {
        var decomposed = new string(s).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    private static long HashShingle(List<Tok> toks, int start, int k)
    {
        // FNV-1a 64-bit over the k normalized words, word-separated so re-groupings don't alias.
        const ulong offset = 14695981039346656037UL, prime = 1099511628211UL;
        ulong h = offset;
        for (int t = 0; t < k; t++)
        {
            foreach (var c in toks[start + t].Norm) { h ^= c; h *= prime; }
            h ^= 0x1F; h *= prime; // unit separator between words
        }
        return unchecked((long)h);
    }

    private static bool TokensEqual(List<Tok> a, int ai, List<Tok> b, int bi, int k)
    {
        for (int t = 0; t < k; t++) if (a[ai + t].Norm != b[bi + t].Norm) return false;
        return true;
    }

    private static TextSpan SpanOf(List<Tok> toks, int start, int wordLen)
    {
        int s = toks[start].Start, e = toks[start + wordLen - 1].End;
        return new TextSpan(s, e - s);
    }

    private static int CountTrue(bool[] flags)
    {
        int c = 0; foreach (var f in flags) if (f) c++; return c;
    }
}

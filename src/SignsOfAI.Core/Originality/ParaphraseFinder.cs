using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Originality;

/// <summary>A pair of sentences that mean the same thing across two documents (a semantic match).</summary>
public sealed record ParaphraseMatch(TextSpan SpanA, TextSpan SpanB, double Similarity, bool AlsoLiteral);

/// <summary>
/// Pairs sentences across two documents by embedding cosine similarity to surface <b>reworded</b> copies —
/// same idea, different words — which the literal (Phase A) shingle check can't see. Input vectors are
/// expected to be L2-normalized, so cosine == dot product. Matches that Phase A already flagged as literal
/// overlap are tagged (<see cref="ParaphraseMatch.AlsoLiteral"/>) so the caller can keep this view additive.
/// Pure and deterministic — the embeddings come from elsewhere; this only compares them.
/// </summary>
public static class ParaphraseFinder
{
    public static IReadOnlyList<ParaphraseMatch> Find(
        IReadOnlyList<TextSpan> sentencesA, float[][] vectorsA,
        IReadOnlyList<TextSpan> sentencesB, float[][] vectorsB,
        double threshold,
        IEnumerable<TextSpan> literalSpansA)
    {
        var literalA = literalSpansA.ToList();

        // Collect every above-threshold pair, then assign greedily by descending similarity so each
        // sentence on either side is used at most once (a clean one-to-one set of best matches).
        var candidates = new List<(int a, int b, double sim)>();
        for (int a = 0; a < sentencesA.Count && a < vectorsA.Length; a++)
            for (int b = 0; b < sentencesB.Count && b < vectorsB.Length; b++)
            {
                double sim = Dot(vectorsA[a], vectorsB[b]);
                if (sim >= threshold) candidates.Add((a, b, sim));
            }

        var usedA = new HashSet<int>();
        var usedB = new HashSet<int>();
        var matches = new List<ParaphraseMatch>();
        foreach (var (a, b, sim) in candidates.OrderByDescending(c => c.sim))
        {
            if (!usedA.Add(a)) continue;
            if (!usedB.Add(b)) { usedA.Remove(a); continue; }
            matches.Add(new ParaphraseMatch(sentencesA[a], sentencesB[b], sim, CoveredByLiteral(sentencesA[a], literalA)));
        }

        return matches.OrderByDescending(m => m.Similarity).ToList();
    }

    private static double Dot(float[] a, float[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        double s = 0;
        for (int i = 0; i < n; i++) s += a[i] * (double)b[i];
        return s;
    }

    /// <summary>True if most of the sentence already sits inside a Phase A literal shared passage.</summary>
    private static bool CoveredByLiteral(TextSpan sentence, List<TextSpan> literal)
    {
        if (sentence.Length <= 0) return false;
        int covered = 0;
        foreach (var l in literal)
        {
            int start = Math.Max(sentence.Start, l.Start);
            int end = Math.Min(sentence.End, l.End);
            if (end > start) covered += end - start;
        }
        return covered >= sentence.Length * 0.6;
    }
}

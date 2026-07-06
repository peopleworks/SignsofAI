namespace SignsOfAI.Web.Services;

public enum DiffKind { Equal, Removed, Added }

public readonly record struct DiffSegment(DiffKind Kind, string Text);

/// <summary>
/// A word-level diff (LCS) used to show what the "Humanize" rewrite changed: removed words on the
/// original side, added words on the rewritten side.
/// </summary>
public static class WordDiff
{
    public static IReadOnlyList<DiffSegment> Compute(string before, string after)
    {
        var a = Tokenize(before);
        var b = Tokenize(after);
        int n = a.Length, m = b.Length;

        // Longest common subsequence table.
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var segments = new List<DiffSegment>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { segments.Add(new(DiffKind.Equal, a[x])); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) { segments.Add(new(DiffKind.Removed, a[x])); x++; }
            else { segments.Add(new(DiffKind.Added, b[y])); y++; }
        }
        while (x < n) segments.Add(new(DiffKind.Removed, a[x++]));
        while (y < m) segments.Add(new(DiffKind.Added, b[y++]));

        return Coalesce(segments);
    }

    // Merge runs of the same kind into one space-joined chunk for clean rendering.
    private static List<DiffSegment> Coalesce(List<DiffSegment> segments)
    {
        var outp = new List<DiffSegment>();
        foreach (var s in segments)
        {
            if (outp.Count > 0 && outp[^1].Kind == s.Kind)
                outp[^1] = new DiffSegment(s.Kind, outp[^1].Text + " " + s.Text);
            else
                outp.Add(s);
        }
        return outp;
    }

    private static string[] Tokenize(string text) =>
        (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

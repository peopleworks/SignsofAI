using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Text;

/// <summary>Computes document-level statistics, most importantly burstiness.</summary>
public static class StatisticsCalculator
{
    public static TextStatistics Compute(TextDocument doc)
    {
        var sentenceLengths = doc.Sentences.Select(s => (double)s.WordCount).ToArray();
        int wordCount = doc.Words.Count;

        double mean = sentenceLengths.Length > 0 ? sentenceLengths.Average() : 0;
        double stdDev = StdDev(sentenceLengths, mean);
        double burstiness = mean > 0 ? stdDev / mean : 0;

        int uniqueWords = doc.Words
            .Select(w => w.Normalized)
            .Distinct()
            .Count();

        return new TextStatistics
        {
            CharCount = doc.Raw.Length,
            WordCount = wordCount,
            SentenceCount = doc.Sentences.Count,
            ParagraphCount = doc.ParagraphCount,
            MeanSentenceLength = Math.Round(mean, 2),
            SentenceLengthStdDev = Math.Round(stdDev, 2),
            Burstiness = Math.Round(burstiness, 3),
            LexicalDiversity = wordCount > 0 ? Math.Round((double)uniqueWords / wordCount, 3) : 0,
            SentenceLengths = doc.Sentences.Select(s => s.WordCount).ToArray(),
        };
    }

    private static double StdDev(double[] values, double mean)
    {
        if (values.Length < 2) return 0;
        double sumSq = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSq / values.Length);
    }
}

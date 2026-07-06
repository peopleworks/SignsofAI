namespace SignsOfAI.Core.Model;

/// <summary>Document-level measurements. Burstiness is the key distributional signal from the research.</summary>
public sealed record TextStatistics
{
    public int CharCount { get; init; }
    public int WordCount { get; init; }
    public int SentenceCount { get; init; }
    public int ParagraphCount { get; init; }

    /// <summary>Mean words per sentence.</summary>
    public double MeanSentenceLength { get; init; }

    /// <summary>Standard deviation of sentence length (in words).</summary>
    public double SentenceLengthStdDev { get; init; }

    /// <summary>
    /// Burstiness = stddev / mean of sentence length (coefficient of variation).
    /// Low (&lt; ~0.4) = machine-like uniformity; high (&gt; ~0.6) = natural human cadence.
    /// </summary>
    public double Burstiness { get; init; }

    /// <summary>Type/token ratio — lexical diversity (unique words / total words).</summary>
    public double LexicalDiversity { get; init; }

    /// <summary>Word count of each sentence, in order — powers the "sentence rhythm" visualization.</summary>
    public IReadOnlyList<int> SentenceLengths { get; init; } = [];
}

using SignsOfAI.Core.Model;
using SignsOfAI.Core.Originality;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class ParaphraseFinderTests
{
    // Unit 2-D vectors so cosine == dot is easy to reason about.
    private static float[] V(double angleTurns)
    {
        double a = angleTurns * 2 * Math.PI;
        return [(float)Math.Cos(a), (float)Math.Sin(a)];
    }

    private static TextSpan S(int start, int len) => new(start, len);

    [Fact]
    public void Matches_high_similarity_pairs_and_ignores_low_ones()
    {
        var sentsA = new[] { S(0, 10), S(20, 10) };
        var sentsB = new[] { S(0, 10), S(20, 10) };
        // A0 ~ B1 (near-parallel), A1 ~ B0 (near-parallel); cross pairs orthogonal.
        var vecA = new[] { V(0.00), V(0.25) };
        var vecB = new[] { V(0.25), V(0.001) };

        var matches = ParaphraseFinder.Find(sentsA, vecA, sentsB, vecB, 0.72, []);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.True(m.Similarity >= 0.72));
    }

    [Fact]
    public void Respects_threshold()
    {
        var sents = new[] { S(0, 10) };
        // ~45° apart ⇒ cosine ≈ 0.707, just under a 0.72 threshold.
        var matches = ParaphraseFinder.Find(sents, [V(0.0)], sents, [V(0.125)], 0.72, []);
        Assert.Empty(matches);
    }

    [Fact]
    public void Assigns_each_sentence_at_most_once()
    {
        // Two A sentences both closest to the single B sentence — only one may claim it.
        var sentsA = new[] { S(0, 10), S(20, 10) };
        var sentsB = new[] { S(0, 10) };
        var vecA = new[] { V(0.0), V(0.01) };
        var vecB = new[] { V(0.0) };

        var matches = ParaphraseFinder.Find(sentsA, vecA, sentsB, vecB, 0.72, []);

        Assert.Single(matches);
        // The closer A sentence (A0, exact parallel) wins.
        Assert.Equal(0, matches[0].SpanA.Start);
    }

    [Fact]
    public void Tags_matches_already_covered_by_a_literal_passage()
    {
        var sentA = S(100, 50);
        var sentsA = new[] { sentA };
        var sentsB = new[] { S(0, 50) };
        var vecA = new[] { V(0.0) };
        var vecB = new[] { V(0.0) };

        // A literal shared passage covering >60% of the sentence ⇒ AlsoLiteral.
        var literal = new[] { S(100, 40) };
        var matches = ParaphraseFinder.Find(sentsA, vecA, sentsB, vecB, 0.72, literal);

        Assert.Single(matches);
        Assert.True(matches[0].AlsoLiteral);
    }

    [Fact]
    public void Does_not_tag_when_literal_overlap_is_small()
    {
        var sentsA = new[] { S(100, 50) };
        var sentsB = new[] { S(0, 50) };
        var vecA = new[] { V(0.0) };
        var vecB = new[] { V(0.0) };

        var literal = new[] { S(100, 10) }; // only 20% of the sentence
        var matches = ParaphraseFinder.Find(sentsA, vecA, sentsB, vecB, 0.72, literal);

        Assert.Single(matches);
        Assert.False(matches[0].AlsoLiteral);
    }

    [Fact]
    public void Empty_inputs_do_not_throw()
    {
        var matches = ParaphraseFinder.Find([], [], [], [], 0.72, []);
        Assert.Empty(matches);
    }
}

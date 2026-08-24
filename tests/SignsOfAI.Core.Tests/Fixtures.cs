using System;
using System.Linq;
using SignsOfAI.Core.Model;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Shared fixture plumbing, and one piece of it is a fact about the engine rather than a convenience.
///
/// Since #59 a document shorter than the shortest text the boundary was measured on gets no verdict
/// at all — 662 words on the corpus this build ships. Almost every fixture in this suite is a
/// paragraph, so without <see cref="LongEnough"/> a test written to check *what the verdict says*
/// silently becomes a test of the length gate, passes for the wrong reason or fails for a reason that
/// has nothing to do with what it was guarding.
///
/// Which is itself the finding: the fixtures were short because the way people use this tool is
/// short, and that is exactly the population the boundary was never measured on.
/// </summary>
internal static class Fixtures
{
    /// <summary>
    /// The passage repeated until it clears <see cref="VerdictBands.MinimumWords"/>, so a test about
    /// the wording of a verdict gets one.
    ///
    /// Repetition rather than filler on purpose: padding with unrelated prose would change what the
    /// rules find and move the score the test is asserting about, while repeating the same sentences
    /// keeps the sentence-length distribution — and therefore the burstiness — roughly where it was.
    /// </summary>
    public static string LongEnough(string text)
    {
        if (VerdictBands.MinimumWords is not { } floor) return text;

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words == 0) return text;

        // A margin over the floor, because the analyzer's word count and this split disagree by a few
        // per cent and a fixture that lands one word short fails somewhere far from here.
        var copies = (int)Math.Ceiling((floor * 1.25) / words);
        return string.Join(" ", Enumerable.Repeat(text, Math.Max(1, copies)));
    }
}

using SignsOfAI.Core.Originality;
using Xunit;

namespace SignsOfAI.Core.Tests;

public class OriginalityCheckerTests
{
    private readonly OriginalityChecker _checker = new(shingleSize: 5, minPassageWords: 6);

    private static OriginalityInput Doc(string title, string text) => new(title, title, text);

    private static PairComparison Pair(OriginalityReport r, string a, string b) =>
        r.Pairs.Single(p =>
            (p.TitleA == a && p.TitleB == b) || (p.TitleA == b && p.TitleB == a));

    [Fact]
    public void Identical_documents_are_full_overlap()
    {
        const string text = "The quick brown fox jumps over the lazy dog while the sun sets slowly behind the hills.";
        var report = _checker.Check([Doc("A", text), Doc("B", text)]);

        var p = report.Pairs.Single();
        Assert.Equal(1.0, p.Overlap, 3);
        Assert.Equal(1.0, p.Jaccard, 3);
        Assert.Single(p.Passages);
    }

    [Fact]
    public void Unrelated_documents_have_no_overlap()
    {
        var report = _checker.Check([
            Doc("A", "Photosynthesis converts sunlight into chemical energy inside the leaves of green plants."),
            Doc("B", "Over the weekend I rebuilt the carburetor on my old motorcycle in the freezing garage."),
        ]);

        var p = report.Pairs.Single();
        Assert.Equal(0, p.Overlap);
        Assert.Empty(p.Passages);
    }

    [Fact]
    public void Detects_copy_disguised_by_case_changes()
    {
        var source = Doc("Source", "The mitochondria is the powerhouse of the cell and it produces energy for the organism.");
        var copy = Doc("Copy", "In my essay THE MITOCHONDRIA IS THE POWERHOUSE OF THE CELL AND IT PRODUCES ENERGY for the body.");
        var report = _checker.Check([source, copy]);

        var p = report.Pairs.Single();
        Assert.True(p.Overlap > 0.5, $"expected strong overlap, got {p.Overlap:P0}");
        Assert.Contains(p.Passages, x => x.WordLength >= 10);
    }

    [Fact]
    public void Detects_spanish_copy_disguised_by_stripped_accents()
    {
        var a = Doc("Bio A", "La fotosíntesis es el proceso mediante el cual las plantas convierten la luz solar en energía.");
        var b = Doc("Bio B", "Segun la teoria, la fotosintesis es el proceso mediante el cual las plantas convierten la luz solar en azucares.");
        var report = _checker.Check([a, b]);

        var p = report.Pairs.Single();
        Assert.True(p.Overlap > 0.5, $"expected accents ignored, got {p.Overlap:P0}");
    }

    [Fact]
    public void Shared_passage_maps_back_to_original_text_verbatim()
    {
        var a = Doc("A", "Intro sentence here. The shared passage is exactly these ten specific words in a row. Outro.");
        var b = Doc("B", "Totally different opening. The shared passage is exactly these ten specific words in a row! Bye.");
        var report = _checker.Check([a, b]);

        var passage = report.Pairs.Single().Passages.OrderByDescending(x => x.WordLength).First();
        // The span into A's original text must slice to real, contiguous source characters.
        var sliceA = passage.SpanA.Slice(a.Text);
        Assert.Contains("shared passage is exactly these ten specific words", sliceA);
    }

    [Fact]
    public void Containment_flags_a_short_copy_inside_a_long_document()
    {
        var quote = "climate change is driven primarily by the burning of fossil fuels worldwide";
        var source = Doc("Source", quote + ".");
        var essay = Doc("Essay",
            "My paper covers many topics over several pages. " +
            "First I discuss history at length with lots of unrelated original wording that I wrote myself. " +
            quote + ". " +
            "Then I continue for many more sentences with entirely my own different thoughts and phrasing here.");
        var report = _checker.Check([source, essay]);

        var p = report.Pairs.Single();
        // The short source is almost entirely contained in the long essay → high containment...
        Assert.True(p.Containment > 0.8, $"containment {p.Containment:P0}");
        // ...even though it is a small fraction of the long essay (coverage asymmetric).
        Assert.True(p.CoverageA > p.CoverageB, "short side should be more covered than the long side");
    }

    [Fact]
    public void Pairs_are_sorted_most_overlapping_first()
    {
        var shared = "renewable energy sources such as solar and wind are reshaping the global power grid";
        var report = _checker.Check([
            Doc("Original", "A completely independent note about baking sourdough bread on a rainy Sunday afternoon."),
            Doc("Source", shared + "."),
            Doc("Copy", "As discussed, " + shared + " in remarkable ways."),
        ]);

        Assert.Equal(3, report.Pairs.Count);
        // The Source↔Copy pair carries the shared text and must rank first.
        var top = report.Pairs[0];
        Assert.Contains("Source", new[] { top.TitleA, top.TitleB });
        Assert.Contains("Copy", new[] { top.TitleA, top.TitleB });
        Assert.True(report.Pairs[0].Overlap >= report.Pairs[1].Overlap);
        Assert.True(report.Pairs[1].Overlap >= report.Pairs[2].Overlap);
    }

    [Fact]
    public void Cohort_surfaces_the_copy_clusters_and_ignores_the_rest()
    {
        var bio = "Photosynthesis is the process by which green plants convert sunlight into chemical energy for growth.";
        var hist = "The French Revolution began in 1789 and reshaped the entire political order of Europe forever.";
        var report = _checker.Check([
            Doc("Ana", bio),
            Doc("Bruno", "In my essay, " + bio + " That is what I learned."),
            Doc("Carla", hist),
            Doc("Diego", "As we know, " + hist + " It mattered a lot."),
            Doc("Elena", "I spent the weekend rebuilding the carburetor on my old motorcycle in the cold garage."),
            Doc("Faisal", "My grandmother browns onions slowly in olive oil before adding cumin to her lentil soup."),
        ]);

        Assert.Equal(15, report.Pairs.Count); // 6 choose 2
        // The two copy clusters must be the two highest-overlap pairs.
        var topTwo = report.Pairs.Take(2).ToList();
        Assert.All(topTwo, p => Assert.True(p.Overlap > 0.5, $"{p.TitleA}<->{p.TitleB} = {p.Overlap:P0}"));
        var topNames = topTwo.SelectMany(p => new[] { p.TitleA, p.TitleB }).ToHashSet();
        Assert.Equal(new[] { "Ana", "Bruno", "Carla", "Diego" }.ToHashSet(), topNames);
        // Everything else is essentially unrelated.
        Assert.All(report.Pairs.Skip(2), p => Assert.True(p.Overlap < 0.2));
    }

    [Fact]
    public void Common_short_phrases_below_threshold_are_ignored()
    {
        // Both mention "on the other hand" (4 words) but share nothing substantial.
        var a = Doc("A", "On the other hand, cats prefer to sleep in warm sunlit corners of the house.");
        var b = Doc("B", "On the other hand, quantum computers exploit superposition to explore many states at once.");
        var report = _checker.Check([a, b]);

        Assert.Empty(report.Pairs.Single().Passages);
    }

    [Fact]
    public void Empty_and_tiny_documents_do_not_throw()
    {
        var report = _checker.Check([Doc("Empty", ""), Doc("Tiny", "hi there"), Doc("Normal", "one two three four five six seven")]);
        Assert.Equal(3, report.Pairs.Count);
        Assert.All(report.Pairs, p => Assert.Equal(0, p.Overlap));
    }
}

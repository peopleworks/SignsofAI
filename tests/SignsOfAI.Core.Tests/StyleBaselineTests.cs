using SignsOfAI.Core.Rules;
using SignsOfAI.Core.Stylometry;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// A deterministic writer. Text is drawn from a fixed weighting over words, so the tests exercise the
/// measurement rather than my prose, and two "authors" differ by exactly what the method claims to
/// read: how often they reach for function words.
///
/// The generator carries its own little pseudo-random sequence instead of <see cref="Random"/> so the
/// fixtures can never shift under a runtime change and quietly turn a real regression into a green run.
/// </summary>
internal sealed class Scribe(int seed, IReadOnlyList<(string Word, int Weight)> profile)
{
    private uint _state = (uint)seed | 1u;

    private uint Next()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    public string Write(int words, IReadOnlyList<string>? topic = null)
    {
        int total = profile.Sum(p => p.Weight);
        var built = new List<string>(words);

        for (int i = 0; i < words; i++)
        {
            // Every eighth word is about the subject matter, which is what changes between one
            // assignment and the next while the function words underneath stay put.
            if (topic is { Count: > 0 } && i % 8 == 7)
            {
                built.Add(topic[(int)(Next() % (uint)topic.Count)]);
                continue;
            }

            int pick = (int)(Next() % (uint)total);
            foreach (var (word, weight) in profile)
            {
                pick -= weight;
                if (pick < 0) { built.Add(word); break; }
            }
        }
        return string.Join(' ', built);
    }
}

/// <summary>
/// The per-writer baseline is the most dangerous thing in this product, because it is the only report
/// that could be misread as naming a culprit. These tests are mostly about the guard rails: that it
/// refuses to answer on thin evidence, that a person writing about something new stays inside their
/// own range, and that no code path can ever return "someone else wrote this".
/// </summary>
public class StyleBaselineTests
{
    // Two writers who differ only in how often they reach for particular function words — the exact
    // signal Burrows's Delta reads, and the one that does not follow the subject.
    private static readonly (string, int)[] Careful =
    [
        ("the", 90), ("of", 60), ("which", 28), ("however", 20), ("that", 42), ("in", 40),
        ("is", 30), ("to", 38), ("and", 34), ("for", 18), ("as", 16), ("with", 15),
        ("this", 14), ("these", 10), ("thus", 9), ("therefore", 8), ("been", 8), ("upon", 6),
        ("a", 20), ("but", 6), ("so", 4), ("just", 2),
    ];

    private static readonly (string, int)[] Plain =
    [
        ("the", 30), ("of", 14), ("which", 2), ("however", 1), ("that", 20), ("in", 22),
        ("is", 34), ("to", 44), ("and", 52), ("for", 14), ("as", 6), ("with", 10),
        ("this", 10), ("these", 3), ("thus", 1), ("therefore", 1), ("been", 4), ("upon", 1),
        ("a", 70), ("but", 34), ("so", 30), ("just", 26),
    ];

    private static readonly string[] EssayTopic = ["rainfall", "catchment", "runoff", "soil", "gauge"];
    private static readonly string[] OtherTopic = ["sonata", "chorus", "tempo", "libretto", "aria"];

    private static List<AuthorSample> Baseline(Scribe scribe, string[]? topic = null, int pieces = 5) =>
        [.. Enumerable.Range(1, pieces).Select(i =>
            new AuthorSample($"b{i}", $"Assignment {i}", scribe.Write(420, topic)))];

    // ---- it refuses to answer on thin evidence -------------------------------------------------------

    [Fact]
    public void Too_little_earlier_work_produces_no_number_at_all()
    {
        var scribe = new Scribe(1, Careful);
        var baseline = Baseline(scribe, EssayTopic, pieces: 2); // ~840 words

        var report = StyleBaseline.Compare(baseline, new AuthorSample("q", "New", scribe.Write(400, EssayTopic)));

        Assert.Equal(BaselinePlacement.Undetermined, report.Placement);
        Assert.False(report.HasResult);
        Assert.NotNull(report.Unavailable);
        Assert.Equal(0, report.Distance);
    }

    [Fact]
    public void A_short_submission_produces_no_number_at_all()
    {
        var scribe = new Scribe(2, Careful);
        var baseline = Baseline(scribe, EssayTopic);

        var report = StyleBaseline.Compare(baseline, new AuthorSample("q", "New", scribe.Write(120, EssayTopic)));

        Assert.Equal(BaselinePlacement.Undetermined, report.Placement);
        Assert.Contains("too short", report.Unavailable!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_input_at_all_is_handled()
    {
        Assert.False(StyleBaseline.Compare(null, null).HasResult);
        Assert.False(StyleBaseline.Compare([], new AuthorSample("q", "q", "hello")).HasResult);
    }

    [Fact]
    public void Every_unavailable_report_still_says_what_to_do()
    {
        var scribe = new Scribe(3, Careful);
        var report = StyleBaseline.Compare(Baseline(scribe, EssayTopic, pieces: 1),
            new AuthorSample("q", "New", scribe.Write(400, EssayTopic)));

        Assert.NotEmpty(report.Advice);
        Assert.Contains("noise", report.Advice, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the measurement --------------------------------------------------------------------------

    [Fact]
    public void The_same_writer_lands_inside_their_own_range()
    {
        var scribe = new Scribe(4, Careful);
        var baseline = Baseline(scribe, EssayTopic);
        var questioned = new AuthorSample("q", "Held out", scribe.Write(420, EssayTopic));

        var report = StyleBaseline.Compare(baseline, questioned);

        Assert.Equal(BaselinePlacement.WithinRange, report.Placement);
        Assert.True(report.Distance <= report.WithinAuthorMax);
        Assert.Equal(5, report.SampleCount);
    }

    [Fact]
    public void The_same_writer_on_a_completely_different_subject_still_lands_inside()
    {
        // The claim that makes function words the right features, and the one that matters most
        // ethically: a student writing about music instead of hydrology has not changed as a writer,
        // and a method that flagged them for changing topic would be worthless and unfair.
        var scribe = new Scribe(5, Careful);
        var baseline = Baseline(scribe, EssayTopic);
        var questioned = new AuthorSample("q", "Different subject", scribe.Write(450, OtherTopic));

        var report = StyleBaseline.Compare(baseline, questioned);

        Assert.NotEqual(BaselinePlacement.BeyondRange, report.Placement);
    }

    [Fact]
    public void A_different_writer_lands_further_out_than_the_writers_own_work()
    {
        var careful = new Scribe(6, Careful);
        var plain = new Scribe(7, Plain);

        var baseline = Baseline(careful, EssayTopic);
        var own = StyleBaseline.Compare(baseline, new AuthorSample("q", "Own", careful.Write(420, EssayTopic)));
        var other = StyleBaseline.Compare(baseline, new AuthorSample("q", "Other", plain.Write(420, EssayTopic)));

        Assert.True(other.Distance > own.Distance,
            $"a different writer measured {other.Distance}, the same writer {own.Distance}");
        Assert.Equal(BaselinePlacement.BeyondRange, other.Placement);
    }

    [Fact]
    public void The_writers_own_pieces_are_measured_the_same_way_as_the_questioned_one()
    {
        // Without leave-one-out, each of the writer's chunks helps compute the statistics it is then
        // scored against, which pulls its distance down, makes their range look tighter than it is and
        // pushes the questioned text further out. That bias runs against the person being asked about.
        var scribe = new Scribe(8, Careful);
        var report = StyleBaseline.Compare(Baseline(scribe, EssayTopic),
            new AuthorSample("q", "Held out", scribe.Write(420, EssayTopic)));

        Assert.NotEmpty(report.WithinAuthorDistances);
        Assert.All(report.WithinAuthorDistances, d => Assert.True(d > 0));
        Assert.Equal(report.WithinAuthorMax, report.WithinAuthorDistances.Max());
    }

    [Fact]
    public void It_names_the_words_doing_the_work()
    {
        // The report has to be checkable, not believable: a reader sees which words moved and by how
        // much, in rates per thousand they can count for themselves.
        var careful = new Scribe(9, Careful);
        var plain = new Scribe(10, Plain);

        var report = StyleBaseline.Compare(Baseline(careful, EssayTopic),
            new AuthorSample("q", "Other", plain.Write(420, EssayTopic)));

        Assert.NotEmpty(report.Drivers);
        Assert.All(report.Drivers, d => Assert.False(string.IsNullOrWhiteSpace(d.Word)));
        Assert.Contains(report.Drivers, d => d.UsedMore);
        Assert.Contains(report.Drivers, d => !d.UsedMore);

        // Ordered by how far out they are, most extreme first.
        var magnitudes = report.Drivers.Select(d => Math.Abs(d.ZScore)).ToList();
        Assert.Equal(magnitudes.OrderByDescending(m => m), magnitudes);
    }

    [Fact]
    public void It_counts_the_words_used_at_a_rate_the_writer_never_uses()
    {
        // Delta is a mean across every feature, so a few words used at wildly different rates get
        // diluted by dozens that match. This count does not average anything, and a reader can check
        // any one of it by counting words.
        var careful = new Scribe(18, Careful);
        var plain = new Scribe(19, Plain);
        var baseline = Baseline(careful, EssayTopic);

        var own = StyleBaseline.Compare(baseline, new AuthorSample("q", "Own", careful.Write(420, EssayTopic)));
        var other = StyleBaseline.Compare(baseline, new AuthorSample("q", "Other", plain.Write(420, EssayTopic)));

        Assert.True(other.WordsOutsideOwnRange > own.WordsOutsideOwnRange,
            $"a different writer put {other.WordsOutsideOwnRange} words outside the range, the same writer {own.WordsOutsideOwnRange}");
        Assert.Contains(other.Drivers, d => d.OutsideOwnRange);
        Assert.All(other.Drivers, d => Assert.True(d.BaselineLowest <= d.BaselineHighest));
    }

    [Fact]
    public void That_count_does_not_decide_the_placement()
    {
        // Deliberate, and worth a test so nobody wires it in later as an improvement. On the few
        // documents measured, a writer's own work put 0-3 words of ~80 outside their range and a
        // plainly different voice put 14 of 93 — a real separation and nowhere near enough evidence to
        // put a cut point on. Placement stays decided by one rule anyone can restate; the count is a
        // reported fact a person reads.
        var scribe = new Scribe(20, Careful);
        var report = StyleBaseline.Compare(Baseline(scribe, EssayTopic),
            new AuthorSample("q", "Held out", scribe.Write(420, EssayTopic)));

        Assert.Equal(report.Distance <= report.WithinAuthorMax, report.Placement == BaselinePlacement.WithinRange);
    }

    [Fact]
    public void Samples_that_disagree_with_each_other_are_flagged_as_a_weak_comparison()
    {
        // Usually it means the pieces are of different kinds — or that one of them is not by the same
        // person, which is worth knowing before anyone reads anything into the rest.
        var careful = new Scribe(11, Careful);
        var plain = new Scribe(12, Plain);

        var mixed = new List<AuthorSample>
        {
            new("b1", "One", careful.Write(430, EssayTopic)),
            new("b2", "Two", careful.Write(430, EssayTopic)),
            new("b3", "Three", careful.Write(430, EssayTopic)),
            new("b4", "Four", plain.Write(430, EssayTopic)),   // not the same hand
            new("b5", "Five", careful.Write(430, EssayTopic)),
        };

        var report = StyleBaseline.Compare(mixed, new AuthorSample("q", "New", careful.Write(420, EssayTopic)));

        Assert.True(report.BaselineIsBroad);
        Assert.Contains("weak", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    // ---- what it will never say ----------------------------------------------------------------------

    [Fact]
    public void There_is_no_value_meaning_someone_else_wrote_this()
    {
        // Structural, and the point of the whole design. The measurement cannot support that claim, so
        // the type system does not offer a way to express it.
        var names = Enum.GetNames<BaselinePlacement>();

        Assert.Equal(["Undetermined", "WithinRange", "AtTheEdge", "BeyondRange"], names);
    }

    [Fact]
    public void Every_result_carries_the_caveat_with_it()
    {
        var scribe = new Scribe(13, Careful);
        var report = StyleBaseline.Compare(Baseline(scribe, EssayTopic),
            new AuthorSample("q", "New", scribe.Write(420, EssayTopic)));

        Assert.Contains("never a conclusion", report.Advice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("settles a suspicion", report.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_distance_is_never_reported_without_its_scale()
    {
        var scribe = new Scribe(14, Careful);
        var report = StyleBaseline.Compare(Baseline(scribe, EssayTopic),
            new AuthorSample("q", "New", scribe.Write(420, EssayTopic)));

        // On its own the distance means nothing, so the sentence carries the writer's own spread too.
        Assert.Contains(report.Distance.ToString("0.###"), report.Summary, StringComparison.Ordinal);
        Assert.Contains(report.WithinAuthorMax.ToString("0.###"), report.Summary, StringComparison.Ordinal);
    }

    // ---- language ------------------------------------------------------------------------------------

    [Fact]
    public void Spanish_has_its_own_function_words_and_they_come_from_the_pack()
    {
        var pack = RulePackLoader.Load("es");

        Assert.NotNull(pack.FunctionWords);
        Assert.Contains("aunque", pack.FunctionWords!);
        Assert.Contains("sin embargo".Split(' ')[0], pack.FunctionWords!);
        Assert.DoesNotContain("however", pack.FunctionWords!);
    }

    // A Spanish writer, built the same way out of Spanish function words.
    private static readonly (string, int)[] Castellano =
    [
        ("de", 90), ("la", 62), ("que", 55), ("el", 50), ("y", 44), ("en", 40), ("los", 26),
        ("se", 24), ("del", 20), ("las", 18), ("por", 17), ("con", 16), ("para", 15),
        ("una", 14), ("es", 13), ("no", 12), ("aunque", 9), ("sin", 8), ("sobre", 7),
        ("entre", 6), ("cuando", 5), ("porque", 5), ("pero", 10), ("como", 11),
    ];

    [Fact]
    public void The_wording_comes_from_the_pack_so_Spanish_reads_in_Spanish()
    {
        var pack = RulePackLoader.Load("es");
        var scribe = new Scribe(15, Castellano);

        var report = StyleBaseline.Compare(Baseline(scribe), new AuthorSample("q", "Nuevo", scribe.Write(420)), "es", pack);

        Assert.True(report.HasResult, report.Unavailable);
        Assert.Contains("Distancia", report.Summary, StringComparison.Ordinal);
        Assert.Contains("nunca una conclusi", report.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void The_wrong_language_says_so_instead_of_asking_for_more_text()
    {
        // English writing measured with Spanish function words finds nothing to measure. Reporting
        // "not enough of this writer's work" would send someone off to gather more text that could
        // not possibly help.
        var pack = RulePackLoader.Load("es");
        var scribe = new Scribe(17, Careful);

        var report = StyleBaseline.Compare(Baseline(scribe, EssayTopic),
            new AuthorSample("q", "New", scribe.Write(420, EssayTopic)), "es", pack);

        Assert.False(report.HasResult);
        Assert.Contains("idioma", report.Unavailable!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_substituted_letter_cannot_shift_the_measurement()
    {
        // Published work attacks author attribution with exactly these characters. Everything is
        // measured on the normalized copy, so a Cyrillic "е" scattered through the baseline changes
        // nothing.
        var scribe = new Scribe(16, Careful);
        var baseline = Baseline(scribe, EssayTopic);
        var questioned = new AuthorSample("q", "New", scribe.Write(420, EssayTopic));

        var cyrillicE = char.ConvertFromUtf32(0x0435);
        var tampered = baseline
            .Select(b => b with { Text = b.Text.Replace("the", "th" + cyrillicE, StringComparison.Ordinal) })
            .ToList();

        var clean = StyleBaseline.Compare(baseline, questioned);
        var attacked = StyleBaseline.Compare(tampered, questioned);

        Assert.Equal(clean.Distance, attacked.Distance);
        Assert.Equal(clean.Placement, attacked.Placement);
    }
}

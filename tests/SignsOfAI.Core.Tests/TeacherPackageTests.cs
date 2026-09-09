using SignsOfAI.Core.Calibration;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The teacher package is the artefact aimed at the person who has to act on a score, and its whole
/// argument is one paragraph: <em>this project can afford to say all that because it publishes how
/// often it is wrong</em>. That paragraph is typed by hand.
///
/// It was wrong in five ways at once. Six weeks after the corpus grew and the boundary moved it
/// still said ninety texts, none flagged, and a bound of 4.1% -- and that neither language supported
/// a threshold of its own, which stopped being true when English earned one at 0.6.0. #85 fixed the
/// same rot on <c>why.html</c> and never looked here, because nothing pointed from one to the other.
///
/// So these read the same <c>published-calibration.json</c> the engine ships and require the page to
/// agree with it, the way <see cref="WhyPageTests"/> does for the page teachers land on first.
/// </summary>
public class TeacherPackageTests
{
    // Whitespace-normalised, because the file is hand-wrapped Markdown and a phrase the guard checks
    // may straddle a line break. A guard that fails when a paragraph is reflowed is a guard someone
    // will weaken rather than satisfy.
    private static readonly string Page = System.Text.RegularExpressions.Regex.Replace(
        File.ReadAllText(PagePath()), @"\s+", " ");

    private static PublishedCalibration Shipped =>
        PublishedCalibration.Current ?? throw new InvalidOperationException(
            "The build carries no published calibration, so the teacher package cannot be checked.");

    private static string PagePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Docs", "Teaching", "README.md");
    }

    // The package is Spanish, so every figure is written with a decimal comma.
    private static string Es(double value, string format) =>
        value.ToString(format, System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    private static void Must(string text, string what)
        => Assert.True(Page.Contains(text, StringComparison.Ordinal),
            $"Docs/Teaching/README.md no longer states {what} (\"{text}\"). The calibration moved; "
            + "rewrite the paragraph under \"Por qué este proyecto puede permitirse decir todo esto\".");

    [Fact]
    public void The_corpus_it_quotes_is_the_one_the_engine_ships()
    {
        Must($"{Shipped.Texts} textos", "the corpus size");
        Must($"por debajo del {Es(Shipped.RateHigh * 100, "0.0")} %", "the upper bound of the interval");
        Must($"el {Es(Shipped.FlaggedAtThreshold / (double)Shipped.Texts * 100, "0.0")} % observado",
            "the observed rate");
    }

    [Fact]
    public void It_does_not_go_back_to_claiming_nothing_was_flagged()
    {
        // The specific sentence that outlived its own truth. Two of the 296 are flagged at 30, and a
        // page arguing that this project publishes its errors must not round its own down to zero.
        Assert.True(Shipped.FlaggedAtThreshold > 0,
            "Nothing is flagged any more, so this guard and the paragraph it guards both need rewriting.");
        Assert.DoesNotContain("ninguno marcado", Page, StringComparison.Ordinal);
    }

    [Fact]
    public void What_it_says_about_each_language_is_what_the_calibration_says()
    {
        var spanish = Shipped.For("es");
        var english = Shipped.For("en");
        Assert.NotNull(spanish);
        Assert.NotNull(english);

        // Spanish has no threshold of its own, so its best bound is the honest figure. English has
        // one, so the figure beside it must be the bound *at that threshold* -- quoting the best
        // bound next to a threshold is the error a reviewer caught in the report on 1 September.
        Assert.Null(spanish!.RecommendedThreshold);
        Must($"son {spanish.Texts} textos", "the size of the Spanish corpus");
        Must($"del {Es(spanish.BestBound * 100, "0.0")} %", "the best bound Spanish allows");

        Assert.NotNull(english!.RecommendedThreshold);
        Assert.NotNull(english.RateHighAtThreshold);
        Must($"con {english.Texts} textos", "the size of the English corpus");
        Must($"del {Es(english.RateHighAtThreshold!.Value * 100, "0.0")} %",
            "the English bound at its own threshold");
    }
}

using System.Text.RegularExpressions;
using SignsOfAI.Core.Calibration;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// <c>why.html</c> is the page README line 15 sends teachers to first, and it has no build step
/// behind it on purpose — it must open from a downloads folder years from now on a machine that
/// has never heard of this project. The cost of that choice is that its figures are typed by hand,
/// and a comment asking the next person to remember is not a guarantee.
///
/// It was not. The page sat from 5 August through two corpus changes still showing "ninety texts"
/// and a green zero, while the project's own published rate had stopped being zero — on the one
/// page whose entire argument is that this project tells you how often it is wrong.
///
/// So these tests read the same <c>published-calibration.json</c> the engine ships and require the
/// page to agree with it. When the calibration moves, this fails by name and says what to redraw.
/// </summary>
public class WhyPageTests
{
    private static readonly string Page = File.ReadAllText(PagePath());

    /// <summary>The snapshot the engine ships. A build without one is a broken build, not a pass.</summary>
    private static PublishedCalibration Shipped =>
        PublishedCalibration.Current ?? throw new InvalidOperationException(
            "The build carries no published calibration, so why.html cannot be checked against it.");

    private static string PagePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "SignsOfAI.Web", "wwwroot", "why.html");
    }

    // The page writes Spanish figures with a comma and English ones with a dot, so every check
    // wants both spellings and both must be present — one language quietly going stale is the
    // failure this guards.
    private static void AssertBothLanguagesSay(string english, string spanish, string what)
    {
        Assert.True(Page.Contains(english, StringComparison.Ordinal),
            $"why.html no longer states {what} in English ({english}). The calibration moved; redraw the page.");
        Assert.True(Page.Contains(spanish, StringComparison.Ordinal),
            $"why.html no longer states {what} in Spanish ({spanish}). The calibration moved; redraw the page.");
    }

    [Fact]
    public void The_corpus_size_on_the_page_is_the_corpus_size_that_shipped()
    {
        int texts = Shipped.Texts;

        AssertBothLanguagesSay($"{texts} HUMAN TEXTS", $"{texts} TEXTOS HUMANOS", "the corpus size in its chart");
        AssertBothLanguagesSay($"{texts} texts written before", $"{texts} textos escritos antes", "the corpus size in its prose");
    }

    [Fact]
    public void The_dot_chart_draws_one_circle_per_text_and_fills_the_flagged_ones()
    {
        // Both halves of the page carry the same chart, so every count here is doubled.
        int texts = Shipped.Texts;
        int flagged = Shipped.FlaggedAtThreshold;

        int circles = Regex.Matches(Page, @"<circle cx=""\d+"" cy=""\d+"" r=""3\.2""").Count;
        int filled = Regex.Matches(Page, @"<circle cx=""\d+"" cy=""\d+"" r=""3\.2"" fill=""#f59e0b""").Count;

        Assert.Equal(texts * 2, circles);
        Assert.Equal(flagged * 2, filled);
    }

    [Fact]
    public void The_upper_bound_drawn_is_the_upper_bound_published()
    {
        // The bound the page may claim is the one at the recommended threshold, never a better one
        // measured at a stricter cut. Printing the best bound beside the recommended threshold is
        // the defect the committee caught in the evidence report on 1 September.
        string en = (Shipped.RateHigh * 100).ToString("0.0");
        AssertBothLanguagesSay($"less than {en}%", $"menos del {en.Replace('.', ',')} %", "the upper bound of its interval");
    }

    [Fact]
    public void The_measured_rate_drawn_is_the_measured_rate_published()
    {
        var c = Shipped;
        string rate = (c.FlaggedAtThreshold / (double)c.Texts * 100).ToString("0.0");

        AssertBothLanguagesSay($">{rate}%<", $">{rate.Replace('.', ',')} %<", "the rate it actually measured");
    }

    [Fact]
    public void The_english_bar_shows_the_bound_at_the_threshold_not_the_best_one()
    {
        var english = Shipped.Languages.Single(l => l.Language == "en");
        Assert.NotNull(english.RateHighAtThreshold);

        string atThreshold = (english.RateHighAtThreshold!.Value * 100).ToString("0.0");
        string best = (english.BestBound * 100).ToString("0.0");

        AssertBothLanguagesSay($">{atThreshold}%<", $">{atThreshold.Replace('.', ',')} %<",
            "English's bound at the recommended threshold");

        // And it must not quietly show the flattering one instead.
        Assert.DoesNotContain($"font-weight=\"700\">{best}%<", Page, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_names_the_engine_that_produced_its_figures()
    {
        string engine = Shipped.Engine;

        AssertBothLanguagesSay($"engine {engine}", $"motor {engine}", "the engine version its figures came from");
    }

    [Fact]
    public void Nothing_on_the_page_still_claims_a_rate_of_zero()
    {
        // The claim that survived six days after it stopped being true.
        Assert.DoesNotContain("zero out of ninety", Page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cero de noventa", Page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_page_still_asks_nothing_of_any_server()
    {
        // Its own first rule: a page arguing that student work never leaves your machine cannot
        // itself fetch from somebody else's. Guarded here because a redraw is when it would slip.
        Assert.DoesNotContain("<script", Page, StringComparison.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(Page, @"(?:src|href)=""(https?:)?//([^""]+)""", RegexOptions.IgnoreCase))
        {
            string host = m.Groups[2].Value;
            bool allowed = host.StartsWith("github.com/peopleworks/", StringComparison.OrdinalIgnoreCase);
            Assert.True(allowed, $"why.html would fetch from {host}; it must ask nothing of any server.");
        }
    }
}

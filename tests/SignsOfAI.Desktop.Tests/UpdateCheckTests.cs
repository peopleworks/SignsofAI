using System.Linq;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop.Tests;

/// <summary>
/// Picking the newest desktop build out of a list of release tags.
///
/// The network half of the check is not tested here — it is a GET and a try/catch, and a test that
/// stands up a fake GitHub would be testing HttpClient. What is tested is the half that can be
/// quietly wrong for a year: an app that sorts versions as text tells everyone they are up to date
/// from 0.10.0 onward, and nobody reports a message that never appears.
/// </summary>
public class UpdateCheckTests
{
    [Fact]
    public void The_newest_is_the_highest_version_not_the_last_line()
    {
        // Deliberately out of order, and with the interleaved NuGet line this repository publishes:
        // ordering by position or by the newest release overall picks `v0.9.0`, which has no app.
        string[] tags =
        [
            "desktop-v0.3.0", "v0.4.0", "desktop-v0.4.0", "v0.9.0", "desktop-v0.2.0",
        ];

        Assert.Equal("0.4.0", DesktopRelease.Newest(tags));
    }

    [Fact]
    public void Ten_is_after_four_and_not_before_it()
    {
        // The bug that would have hidden every update for the rest of the project's life.
        Assert.Equal("0.10.0", DesktopRelease.Newest(["desktop-v0.4.0", "desktop-v0.10.0"]));
        Assert.Equal("1.0.0", DesktopRelease.Newest(["desktop-v0.10.0", "desktop-v1.0.0"]));
    }

    [Theory]
    [InlineData("desktop-v0.5.0-rc.1")]   // a prerelease is not something to send a teacher to
    [InlineData("desktop-vnext")]
    [InlineData("desktop-v")]
    [InlineData("v0.4.0")]                // the package line, not this one
    [InlineData("random-tag")]
    public void What_it_cannot_parse_it_ignores(string tag) =>
        Assert.Null(DesktopRelease.Newest([tag]));

    [Fact]
    public void An_empty_or_unrelated_list_produces_nothing()
    {
        Assert.Null(DesktopRelease.Newest([]));
        Assert.Null(DesktopRelease.Newest(["v0.1.0", "v0.2.0"]));
    }

    [Theory]
    [InlineData("0.5.0", "0.4.0", true)]
    [InlineData("0.4.0", "0.4.0", false)]   // current: say nothing at all
    [InlineData("0.4.0", "0.5.0", false)]   // ahead of the release: also nothing
    [InlineData("0.5.0", "1.0.0", false)]   // a developer build reports the SDK's 1.0.0
    [InlineData("0.5.0", null, false)]      // no idea what is running: do not guess
    [InlineData(null, "0.4.0", false)]
    public void Newer_means_strictly_newer(string? candidate, string? running, bool expected) =>
        Assert.Equal(expected, DesktopRelease.IsNewerThan(candidate, running));

    /// <summary>
    /// The link a notice offers is a page to read, never a file to fetch. The build is unsigned, and
    /// an app that hands somebody a download it chose is one step from an app that runs it.
    /// </summary>
    [Fact]
    public void The_notice_links_to_the_notes_and_not_to_a_zip()
    {
        var url = DesktopRelease.ReleasePageFor("0.5.0");

        Assert.Equal("https://github.com/peopleworks/SignsofAI/releases/tag/desktop-v0.5.0", url);
        Assert.DoesNotContain(".zip", url);
        Assert.DoesNotContain("/download/", url);
    }

    /// <summary>
    /// The browser is never out of date, so it offers none of this and the interface renders nothing.
    /// </summary>
    [Fact]
    public async Task A_browser_tab_has_nothing_to_check()
    {
        var browser = new NoUpdateCheck();

        Assert.False(browser.IsAvailable);
        Assert.Equal(UpdateStatus.Nothing, await browser.CheckAsync());
    }

    [Fact]
    public void The_desktop_host_does_offer_it() => Assert.True(new GitHubUpdateCheck().IsAvailable);
}

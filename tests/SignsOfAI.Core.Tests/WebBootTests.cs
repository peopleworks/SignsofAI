using System.Text.RegularExpressions;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Guards how the web app starts, in <c>src/SignsOfAI.Web/wwwroot/</c>.
///
/// The runtime arrives as roughly fifty files, each pinned by a SHA-256 in the boot manifest. When
/// the host answers one of them with a 503, the browser hashes the error page instead of the
/// assembly, the integrity check fails, and Blazor stops for good — before it owns an error UI, so
/// the visitor gets the loading circle and nothing else. That is a healthy deployment reading as an
/// outage, and it is how it was first reported to us.
///
/// The retry itself is tested where it can actually be executed: <c>tests/boot/boot.test.mjs</c>
/// runs <c>boot.js</c> against a fake network. What is left here is the wiring those tests cannot
/// see — which file loads in which order, and whether the explanation can still be shown when the
/// file holding it is the one that never arrived.
///
/// Everything is asserted against source with comments stripped. An earlier version of these tests
/// searched the raw text, and a reviewer showed every one of them could be satisfied by a comment
/// while the real markup said the opposite.
/// </summary>
public class WebBootTests
{
    private static readonly string WebRoot = FindWebRoot();

    private static readonly string IndexHtml =
        StripComments(File.ReadAllText(Path.Combine(WebRoot, "index.html")));

    [Fact]
    public void Blazor_does_not_autostart_so_boot_js_can_supply_the_retry()
    {
        Assert.Contains("blazor.webassembly#[.{fingerprint}].js\" autostart=\"false\"", IndexHtml);
    }

    [Fact]
    public void Boot_script_is_loaded_after_blazor_defines_itself()
    {
        var blazor = IndexHtml.IndexOf("blazor.webassembly#[.{fingerprint}].js", StringComparison.Ordinal);
        var boot = IndexHtml.IndexOf("js/boot.js", StringComparison.Ordinal);

        Assert.True(blazor >= 0, "index.html no longer loads the Blazor WebAssembly script.");
        Assert.True(boot >= 0, "index.html no longer loads js/boot.js, so nothing starts the app.");
        Assert.True(boot > blazor,
            "boot.js must come after the Blazor script: it calls Blazor.start(), and window.Blazor " +
            "does not exist until that script has run.");
    }

    [Fact]
    public void The_failure_panel_stays_inline_so_it_survives_its_own_file_not_arriving()
    {
        // The point of the whole exercise. Move this into a .js file and the one failure it could
        // never explain becomes its own: a 503 on that request leaves the eternal spinner back.
        var panel = IndexHtml.IndexOf("window.signsofaiBoot", StringComparison.Ordinal);
        var firstScriptFile = IndexHtml.IndexOf("js/boot.js", StringComparison.Ordinal);

        Assert.True(panel >= 0, "The inline boot panel is gone from index.html.");
        Assert.True(panel < firstScriptFile,
            "The panel must be defined before any script it has to survive the loss of.");
    }

    [Fact]
    public void A_watchdog_covers_the_failures_the_retry_cannot_reach()
    {
        // The runtime's ES modules must be left to the default loader, so they get no retry; and a
        // connection that hangs never errors. Only a watchdog catches those.
        Assert.Contains("setInterval", IndexHtml);
        Assert.Contains("lastProgress", IndexHtml);
    }

    [Fact]
    public void The_panel_offers_the_desktop_app_by_absolute_url_not_an_in_app_route()
    {
        // Every route on this site is served by this same page, so a link to /download would try to
        // start the app it just failed to start. The escape hatch has to leave the site.
        var match = Regex.Match(IndexHtml, @"var DESKTOP = '([^']+)'");

        Assert.True(match.Success, "The panel no longer names a desktop download URL.");
        Assert.StartsWith("https://github.com/", match.Groups[1].Value);
    }

    [Fact]
    public void The_panel_prefers_the_language_the_reader_actually_chose()
    {
        // The app persists the switch here. Reading only navigator.language would show an English
        // panel to a teacher using the app in Spanish — issue #36 in the one place Loc cannot reach.
        Assert.Contains("signsofai.ui.lang", IndexHtml);
    }

    [Theory]
    [InlineData("No se pudo completar la carga")]
    [InlineData("Couldn’t load")]
    public void The_panel_speaks_both_languages(string phrase)
    {
        Assert.Contains(phrase, IndexHtml);
    }

    /// <summary>
    /// Removes HTML comments and JavaScript line comments, so an assertion cannot be satisfied by
    /// prose describing what the markup ought to do. <c>//</c> inside a URL is left alone.
    /// </summary>
    private static string StripComments(string source)
    {
        var withoutHtml = Regex.Replace(source, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutHtml, @"(?<!:)//[^\r\n]*", string.Empty);
    }

    private static string FindWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SignsOfAI.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "src", "SignsOfAI.Web", "wwwroot");
        Assert.True(Directory.Exists(path), $"Expected the web wwwroot at {path}");
        return path;
    }
}

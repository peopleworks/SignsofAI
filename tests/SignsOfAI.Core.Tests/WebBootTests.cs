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
/// <c>boot.js</c> retries, and says so when the retry does not help. Three things in it fail
/// silently if a later edit gets them wrong, which is why they are asserted here rather than left
/// to a reviewer: the script has to run *after* Blazor is defined, it has to keep handing the
/// manifest hash to <c>fetch</c>, and it has to leave the runtime's own ES modules alone.
/// </summary>
public class WebBootTests
{
    private static readonly string WebRoot = FindWebRoot();
    private static readonly string IndexHtml =
        File.ReadAllText(Path.Combine(WebRoot, "index.html"));
    private static readonly string BootJs =
        File.ReadAllText(Path.Combine(WebRoot, "js", "boot.js"));

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
    public void Boot_script_starts_blazor_through_a_boot_resource_loader()
    {
        Assert.Contains("Blazor.start(", BootJs);
        Assert.Contains("loadBootResource", BootJs);
    }

    [Fact]
    public void Retry_still_hands_the_manifest_hash_to_fetch()
    {
        // Returning a Response from loadBootResource takes the integrity check away from Blazor.
        // Dropping `integrity` here would therefore not fail anything — it would quietly stop
        // verifying every assembly the app loads, which is the opposite of what this file is for.
        Assert.Contains("integrity: integrity", BootJs);
    }

    [Fact]
    public void Runtime_modules_are_left_to_the_default_loader()
    {
        // The dotnet.js family is imported as ES modules, which needs a URL. Answer those with a
        // Response and the import fails — turning a fix for rare 503s into a permanent breakage.
        Assert.Contains("dotnetjs", BootJs);
        Assert.Contains("undefined", BootJs);
    }

    [Theory]
    // The app picks its language from the browser, and this panel renders before the app exists,
    // so it carries its own copy. English-only here would be the #36 bug in a new place.
    [InlineData("No se pudo terminar de cargar")]
    [InlineData("Couldn't finish loading")]
    public void Failure_panel_speaks_both_languages(string phrase)
    {
        Assert.Contains(phrase, BootJs);
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

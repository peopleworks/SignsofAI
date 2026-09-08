using System.Text.RegularExpressions;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The deploy publishes two applications onto one host: the web app at the site root, and the Word
/// task pane in a subdirectory of it. A real directory beats a client-side route, so any page whose
/// <c>@page</c> path matches that subdirectory is unreachable — the visitor gets the task pane,
/// which outside Word says it has no document to read.
///
/// That happened. <c>/word</c> was given to a page while the deploy was already publishing the pane
/// at <c>/word/</c>, and the navigation link led to the pane for as long as it took to notice. It is
/// invisible locally, because a dev server has neither directory, and invisible in the diff, because
/// the two halves live in different files.
/// </summary>
public class DeployedPathTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>Every route the router owns, from the <c>@page</c> directives themselves.</summary>
    private static IReadOnlyList<string> Routes()
    {
        string pages = Path.Combine(RepoRoot(), "src", "SignsOfAI.UI", "Pages");
        var routes = new List<string>();

        foreach (string file in Directory.EnumerateFiles(pages, "*.razor"))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"^@page\s+""/([^""]*)""", RegexOptions.Multiline))
                routes.Add(m.Groups[1].Value.Trim('/'));

        Assert.NotEmpty(routes);
        return routes;
    }

    /// <summary>
    /// The directory the deploy copies the pane into, taken from the workflow rather than repeated
    /// here — the point of this test is that the two are compared, not that both are typed twice.
    /// </summary>
    private static string PaneDirectory()
    {
        string workflow = File.ReadAllText(
            Path.Combine(RepoRoot(), ".github", "workflows", "deploy-pages.yml"));

        var m = Regex.Match(workflow, @"cp -r publish-word/wwwroot publish/wwwroot/(\S+)");
        Assert.True(m.Success,
            "deploy-pages.yml no longer copies the task pane where this test expects. If the step " +
            "moved or was renamed, update this pattern — do not delete the check.");

        return m.Groups[1].Value.Trim('/');
    }

    [Fact]
    public void No_page_route_is_shadowed_by_the_task_pane_directory()
    {
        string pane = PaneDirectory();
        var clash = Routes().FirstOrDefault(r => string.Equals(r, pane, StringComparison.OrdinalIgnoreCase));

        Assert.True(clash is null,
            $"A page is routed at \"/{clash}\" and the deploy publishes the task pane at \"/{pane}/\". " +
            "On the host the directory wins and the page is unreachable — visitors get the pane. " +
            "Move the page's route; the pane's address is in a manifest people have installed.");
    }

    [Fact]
    public void The_manifest_points_at_the_directory_the_deploy_publishes()
    {
        // The other half of the same seam: the pane's URL lives in the manifest Word loads, and the
        // deploy decides where the files land. If those two disagree the add-in 404s, which Word
        // reports as "this add-in may not load properly" — an error a long way from its cause.
        string manifest = File.ReadAllText(
            Path.Combine(RepoRoot(), "src", "SignsOfAI.Word", "manifest.xml"));

        var source = Regex.Match(manifest, @"<SourceLocation DefaultValue=""([^""]+)""");
        Assert.True(source.Success, "The manifest has no SourceLocation.");

        string path = new Uri(source.Groups[1].Value).AbsolutePath;   // /SignsofAI/word/index.html
        string[] parts = path.Trim('/').Split('/');

        Assert.True(parts.Length >= 2, $"SourceLocation \"{path}\" has no subdirectory to compare.");
        Assert.Equal(PaneDirectory(), parts[^2]);
    }
}

using System.Text.RegularExpressions;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// The version is typed into seven places across five files, and a release bumps them by hand.
/// <c>.claude-plugin/plugin.json</c> was three releases behind — it said 0.4.0 while every channel
/// shipped 0.7.1 — and nothing failed, because the release checklist never listed it.
///
/// That file is what a plugin directory reads, which makes it the version a stranger sees first.
/// So this holds every one of them to the engine's own, and fails by file name when a release
/// forgets one. The eighth time something this project says about itself went stale.
/// </summary>
public class ReleaseVersionTests
{
    private static readonly string Root = FindRoot();

    /// <summary>The authority: what SignsOfAI.Core ships as.</summary>
    private static readonly string Engine = Read(
        Path.Combine("src", "SignsOfAI.Core", "SignsOfAI.Core.csproj"),
        @"<Version>(?<v>[^<]+)</Version>");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Read(string relativePath, string pattern)
    {
        var text = File.ReadAllText(Path.Combine(Root, relativePath));
        var match = Regex.Match(text, pattern);
        Assert.True(match.Success, $"{relativePath} no longer carries a version matching {pattern}.");
        return match.Groups["v"].Value.Trim();
    }

    public static TheoryData<string, string> Declarations() => new()
    {
        // Published to NuGet.
        { Path.Combine("src", "SignsOfAI.Cli", "SignsOfAI.Cli.csproj"), @"<Version>(?<v>[^<]+)</Version>" },
        { Path.Combine("src", "SignsOfAI.Mcp", "SignsOfAI.Mcp.csproj"), @"<Version>(?<v>[^<]+)</Version>" },
        // What the download page offers, which the release workflow also enforces.
        { Path.Combine("src", "SignsOfAI.UI", "Services", "DesktopRelease.cs"),
          @"public const string Version = ""(?<v>[^""]+)"";" },
        // What a plugin directory shows a stranger. This is the one that drifted.
        { Path.Combine(".claude-plugin", "plugin.json"), @"""version"":\s*""(?<v>[^""]+)""" },
    };

    [Theory]
    [MemberData(nameof(Declarations))]
    public void Every_shipped_manifest_declares_the_engine_version(string relativePath, string pattern)
    {
        Assert.True(Engine == Read(relativePath, pattern),
            $"{relativePath} says {Read(relativePath, pattern)} and the engine ships {Engine}. "
            + "A release bumps all of them or none of them.");
    }

    [Fact]
    public void The_mcp_registry_manifest_declares_it_in_both_places()
    {
        // server.json carries the version twice, and publishing with them out of step is rejected by
        // the registry after NuGet has already gone out — the worst moment to find out.
        var path = Path.Combine("src", "SignsOfAI.Mcp", ".mcp", "server.json");
        var found = Regex.Matches(File.ReadAllText(Path.Combine(Root, path)),
            @"""version"":\s*""(?<v>[^""]+)""").Select(m => m.Groups["v"].Value).ToList();

        Assert.Equal(2, found.Count);
        Assert.All(found, v => Assert.Equal(Engine, v));
    }
}

using System.Text.RegularExpressions;
using Xunit;

namespace SignsOfAI.Core.Tests;

/// <summary>
/// Two READMEs describe the MCP server's tools by hand: the repository's own, which is what a
/// visitor reads and what every directory listing copies from, and the server package's.
///
/// The root README went stale and nobody measured it. <c>write_report</c> shipped and its row was
/// never added, so the table listed nine tools and the sentence under it said "the first seven run
/// entirely on the machine" when eight did. Two public directory listings — one merged, one open —
/// then copied that table faithfully and were wrong in the same way, six weeks later, in someone
/// else's repository where we have no guard at all.
///
/// So this reads the server's own attributes and requires both files to agree with them. Adding a
/// tool without documenting it fails here, by name.
/// </summary>
public class McpToolDocumentationTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>
    /// The tools as the server actually declares them. <c>OpenWorld</c> is the flag that means a
    /// tool reaches off the machine, so the on-device/server split is derived, never typed.
    /// </summary>
    private static readonly IReadOnlyList<(string Name, bool ReachesOut)> Tools = ReadTools();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static IReadOnlyList<(string, bool)> ReadTools()
    {
        var toolsDir = Path.Combine(RepoRoot, "src", "SignsOfAI.Mcp", "Tools");
        var attribute = new Regex(@"\[McpServerTool\((?<args>[^\]]*)\)", RegexOptions.Compiled);

        var found = new List<(string, bool)>();
        foreach (var file in Directory.EnumerateFiles(toolsDir, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
        foreach (Match match in attribute.Matches(File.ReadAllText(file)))
        {
            var args = match.Groups["args"].Value;
            var name = Regex.Match(args, @"Name\s*=\s*""(?<n>[^""]+)""");
            if (!name.Success) continue;
            found.Add((name.Groups["n"].Value, args.Contains("OpenWorld = true", StringComparison.Ordinal)));
        }

        Assert.NotEmpty(found);
        return found;
    }

    // The two files spell their counts out in words, so the guard has to as well.
    private static string InWords(int n) => n switch
    {
        1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five",
        6 => "six", 7 => "seven", 8 => "eight", 9 => "nine", 10 => "ten",
        11 => "eleven", 12 => "twelve",
        _ => throw new ArgumentOutOfRangeException(nameof(n),
            $"The server has {n} tools and this guard cannot spell that. Extend it.")
    };

    public static TheoryData<string> DocumentedFiles() => new()
    {
        "README.md",
        Path.Combine("src", "SignsOfAI.Mcp", "README.md"),
    };

    [Theory]
    [MemberData(nameof(DocumentedFiles))]
    public void Every_tool_the_server_declares_is_documented(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, relativePath));

        var missing = Tools.Select(t => t.Name)
                           .Where(name => !text.Contains($"`{name}`", StringComparison.Ordinal))
                           .ToList();

        Assert.True(missing.Count == 0,
            $"{relativePath} does not mention {string.Join(", ", missing)}. The server exposes " +
            $"{Tools.Count} tools; add the row, or the next directory listing will copy the gap.");
    }

    [Fact]
    public void The_root_readme_says_how_many_tools_stay_on_the_machine()
    {
        var onDevice = Tools.Count(t => !t.ReachesOut);
        var reachOut = Tools.Count - onDevice;
        var text = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));

        var expected = $"The first {InWords(onDevice)} run entirely on the machine; " +
                       $"the last {InWords(reachOut)} disclose that they send text to the server";

        Assert.True(text.Contains(expected, StringComparison.Ordinal),
            $"README.md no longer says \"{expected}\". {onDevice} of the {Tools.Count} tools carry no " +
            "OpenWorld flag; correct the sentence under the table.");
    }

    [Fact]
    public void The_server_readme_says_how_many_tools_stay_on_the_machine()
    {
        var onDevice = Tools.Count(t => !t.ReachesOut);
        var text = File.ReadAllText(Path.Combine(RepoRoot, "src", "SignsOfAI.Mcp", "README.md"));

        var expected = $"{char.ToUpperInvariant(InWords(onDevice)[0])}{InWords(onDevice)[1..]} " +
                       $"of the {InWords(Tools.Count)} run";

        Assert.True(text.Contains(expected, StringComparison.Ordinal),
            $"src/SignsOfAI.Mcp/README.md no longer says \"{expected} ...\". The server has " +
            $"{Tools.Count} tools, {onDevice} of them on-device.");
    }
}

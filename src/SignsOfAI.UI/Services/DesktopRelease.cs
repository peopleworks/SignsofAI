namespace SignsOfAI.UI.Services;

/// <summary>
/// Which Windows build the download page offers.
///
/// Written down rather than looked up, and that is the interesting part. GitHub's
/// <c>/releases/latest</c> redirect resolves to whichever release is newest *overall*, and this
/// repository publishes two independent tag lines on purpose — <c>v*</c> ships the NuGet packages,
/// <c>desktop-v*</c> ships this app. About half the time "latest" is therefore a page with no .zip
/// on it, which is a worse answer than a stale one.
///
/// The cost of writing it down is that it can drift from what is actually published, so it is not
/// left to anybody's discipline: <c>.github/workflows/desktop-release.yml</c> refuses to build a
/// <c>desktop-v*</c> tag whose version does not match <see cref="Version"/>. One edit per release,
/// and a release that forgets the edit does not ship.
///
/// Only the version is a constant. The size and the checksum are not, because they are only known
/// after the runner has built the zip — they live in the release notes, which the page links to.
/// </summary>
public static class DesktopRelease
{
    /// <summary>The published version, without the <c>desktop-v</c> prefix its tag carries.</summary>
    public const string Version = "0.5.0";

    /// <summary>
    /// The .zip itself, so the button downloads rather than starting a scavenger hunt through a
    /// release page. Interpolated from <see cref="Version"/> so the two cannot disagree.
    /// </summary>
    public const string ZipUrl =
        $"https://github.com/peopleworks/SignsofAI/releases/download/desktop-v{Version}/SignsOfAI-Desktop-{Version}-win-x64.zip";

    /// <summary>The release page: the notes, the checksum, and what changed since the last one.</summary>
    public const string ReleaseUrl =
        $"https://github.com/peopleworks/SignsofAI/releases/tag/desktop-v{Version}";

    /// <summary>Every desktop release, for somebody who wants an older build or its checksum.</summary>
    public const string AllReleasesUrl =
        "https://github.com/peopleworks/SignsofAI/releases?q=desktop&expanded=true";

    /// <summary>The prefix that separates this tag line from the one that publishes the packages.</summary>
    public const string TagPrefix = "desktop-v";

    /// <summary>
    /// The newest desktop version among a list of tag names, or null if none of them is one.
    ///
    /// Pure, and separated from whatever fetched the list, because this is the half that can be
    /// quietly wrong. Three things it must get right and a naive implementation does not:
    ///
    /// <list type="bullet">
    /// <item><b>Order by version, never by date.</b> A release can be re-cut, and this repository
    /// publishes two tag lines that interleave — the newest release overall is a NuGet one about half
    /// the time.</item>
    /// <item><b>Order numerically.</b> Sorted as text, <c>0.10.0</c> comes before <c>0.4.0</c>, and
    /// the app would tell everyone they were up to date for the rest of the project's life.</item>
    /// <item><b>Ignore what it cannot parse.</b> A prerelease tag is not something to advertise to a
    /// teacher, and an unparseable one is not something to guess at.</item>
    /// </list>
    /// </summary>
    public static string? Newest(IEnumerable<string> tagNames)
    {
        ArgumentNullException.ThrowIfNull(tagNames);

        // `Version` here is this class's own constant, so the type needs its full name.
        System.Version? best = null;
        foreach (var tag in tagNames)
        {
            if (tag is null || !tag.StartsWith(TagPrefix, StringComparison.Ordinal)) continue;
            if (!System.Version.TryParse(tag[TagPrefix.Length..], out var version)) continue;
            if (best is null || version > best) best = version;
        }

        return best?.ToString();
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is strictly newer than the build being run.
    ///
    /// False when either side cannot be parsed, which covers the case that matters: a developer build
    /// reports the SDK's own 1.0.0, and telling a maintainer they are behind because 0.5.0 sorts lower
    /// would be noise. Equal versions are not newer, so a current build says nothing at all.
    /// </summary>
    public static bool IsNewerThan(string? candidate, string? running) =>
        System.Version.TryParse(candidate, out var latest)
        && System.Version.TryParse(running, out var current)
        && latest > current;

    /// <summary>The release page for a version, for a notice that links to the notes and nothing else.</summary>
    public static string ReleasePageFor(string version) =>
        $"https://github.com/peopleworks/SignsofAI/releases/tag/{TagPrefix}{version}";
}

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
    public const string Version = "0.4.0";

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
}

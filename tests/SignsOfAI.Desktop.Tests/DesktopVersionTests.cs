using SignsOfAI.Desktop;
using SignsOfAI.UI.Services;

namespace SignsOfAI.Desktop.Tests;

/// <summary>
/// The desktop app has to be able to say which build it is.
///
/// This is guarding a support message rather than a crash: a user reported that "desktop 0.4.0 is
/// not published" when what they meant was that they could not tell which build they were running.
/// The plumbing that fixes it is three lines and would be easy to delete by accident, and its
/// absence looks like nothing at all — the footer simply says one word less.
/// </summary>
public class DesktopVersionTests
{
    [Fact]
    public void The_running_build_reports_a_version()
    {
        Assert.False(string.IsNullOrWhiteSpace(DesktopVersion.Running()),
            "The desktop assembly carries no informational version, so the footer and the download " +
            "page have nothing to show. A developer build reports the SDK's 1.0.0; getting null here " +
            "means the attribute was suppressed in the .csproj.");
    }

    [Theory]
    [InlineData("0.4.0", "0.4.0")]
    [InlineData("0.4.0+9f2c1ab3", "0.4.0")]                 // the SDK appends the commit
    [InlineData("0.5.0-rc.1+9f2c1ab3", "0.5.0-rc.1")]       // a prerelease keeps its own suffix
    public void Source_control_metadata_is_not_shown_to_the_reader(string informational, string shown) =>
        Assert.Equal(shown, DesktopVersion.Trim(informational));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_is_better_than_an_empty_version(string? informational) =>
        Assert.Null(DesktopVersion.Trim(informational));

    /// <summary>
    /// The two hosts must not describe themselves the same way. The shared footer used to claim
    /// "Blazor WebAssembly · runs 100% in your browser" inside a WPF window, where neither half is
    /// true, and a project that asks people to show evidence cannot be loose about a sentence it
    /// prints on every one of its own pages.
    /// </summary>
    [Fact]
    public void The_desktop_host_describes_itself_as_a_desktop()
    {
        var desktop = HostCapabilities.Desktop("0.4.0");

        Assert.Equal("0.4.0", desktop.Version);
        Assert.True(desktop.ReachesLocalServices);
        Assert.NotEqual(HostCapabilities.Browser.RuntimeKey, desktop.RuntimeKey);

        // A browser tab is always the deployment that was last pushed, so it has no build to name.
        Assert.Null(HostCapabilities.Browser.Version);
    }
}

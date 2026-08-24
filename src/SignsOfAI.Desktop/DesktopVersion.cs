using System.Reflection;

namespace SignsOfAI.Desktop;

/// <summary>
/// Which build this is.
///
/// Small enough to look unnecessary, and it exists because of a support message: somebody reported
/// that "desktop 0.4.0 is not published" when what they actually meant was that they could not tell
/// which build they had. The app said its name in the title bar and nothing else, so neither could
/// anyone trying to help them.
///
/// Kept out of <see cref="MainWindow"/> so it can be tested without standing up a window — same
/// reason DesktopFolderBatch lives on its own.
/// </summary>
public static class DesktopVersion
{
    /// <summary>
    /// The version this executable was built with, or null if it somehow carries none.
    ///
    /// The release workflow passes <c>-p:Version=</c> taken from the tag and the SDK turns that into
    /// <see cref="AssemblyInformationalVersionAttribute"/>. A developer build with no version set
    /// reports the SDK's own 1.0.0, which is the honest answer for something never released.
    /// </summary>
    public static string? Running() => Of(typeof(DesktopVersion).Assembly);

    /// <summary>Same, for a given assembly, so a test can hand it one.</summary>
    public static string? Of(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        // The SDK appends "+<commit sha>" when the repository is available at build time. Accurate,
        // and not what somebody reading a footer is trying to find out.
        return Trim(informational);
    }

    /// <summary>Drops the source-control metadata the SDK appends after a <c>+</c>.</summary>
    public static string? Trim(string? informational) =>
        string.IsNullOrWhiteSpace(informational) ? null : informational.Split('+')[0];
}

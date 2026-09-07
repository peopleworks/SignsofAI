using System.Reflection;

namespace SignsOfAI.Cli;

/// <summary>
/// The version this build actually is, taken from the assembly the compiler stamped from
/// &lt;Version&gt; in the csproj. Restating it in source is how `--version` came to report 0.1.0
/// while the published package was 0.6.0 — seven releases of a tool telling people the wrong
/// answer to the one question they ask when a fix does not seem to be there.
/// </summary>
public static class BuildInfo
{
    public static string Version { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(BuildInfo).Assembly;

        // InformationalVersion carries the full "0.7.0+<sha>" when SourceLink is on; the build
        // metadata after '+' is not part of the version people are asking about.
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational[..plus];
        }

        // A version with a trailing ".0" the csproj never wrote is still the truth about this build.
        return assembly.GetName().Version?.ToString(3) ?? "unknown";
    }
}

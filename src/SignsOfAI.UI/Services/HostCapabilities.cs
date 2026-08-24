namespace SignsOfAI.UI.Services;

/// <summary>
/// What the host running this interface can actually do, for the few places where the honest answer
/// differs between a browser tab and a desktop window.
///
/// This is deliberately a short list of *capabilities* rather than a "am I the desktop?" flag. The
/// interface should branch on what is possible, not on who is asking — the day a third host appears,
/// or a browser stops blocking a thing, the components do not need revisiting.
/// </summary>
public sealed class HostCapabilities
{
    /// <summary>
    /// True when HTTP requests leave the process natively, so a service on the user's own machine —
    /// Ollama on <c>localhost:11434</c> — is simply reachable.
    ///
    /// In a browser it is not: the page is served over HTTPS and the call to a local, plain-HTTP
    /// port is refused unless the user reconfigures Ollama's allowed origins. That is why the
    /// browser build has to explain a workaround and the desktop build must not — repeating it there
    /// would be advice for a problem the user does not have.
    /// </summary>
    public bool ReachesLocalServices { get; init; }

    /// <summary>
    /// The build the user is looking at, when the host is a thing that gets downloaded and can
    /// therefore be out of date. Null in a browser tab, which always serves what was last deployed
    /// and has no version to report.
    ///
    /// It exists because of a support message: somebody reported "desktop 0.4.0 is not published"
    /// when what they meant was "I cannot tell which build I have". The app said its name in the
    /// title bar and nothing else, so neither could anyone helping them.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// The locale key describing how this host runs, for the footer.
    ///
    /// Not decoration: the shared footer claimed "Blazor WebAssembly · runs 100% in your browser"
    /// inside a WPF window, where both halves are false. A tool that asks people to show evidence
    /// cannot be careless about a claim on every one of its own pages.
    /// </summary>
    public string RuntimeKey { get; init; } = "footer.runtime.browser";

    /// <summary>The browser: sandboxed, and the one that has to ask the user for CORS help.</summary>
    public static HostCapabilities Browser { get; } = new() { ReachesLocalServices = false };

    /// <summary>A desktop window: native HTTP, no preflight, localhost included.</summary>
    public static HostCapabilities Desktop(string? version) => new()
    {
        ReachesLocalServices = true,
        RuntimeKey = "footer.runtime.desktop",
        Version = version,
    };
}

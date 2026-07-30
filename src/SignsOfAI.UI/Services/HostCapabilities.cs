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

    /// <summary>The browser: sandboxed, and the one that has to ask the user for CORS help.</summary>
    public static HostCapabilities Browser { get; } = new() { ReachesLocalServices = false };

    /// <summary>A desktop window: native HTTP, no preflight, localhost included.</summary>
    public static HostCapabilities Desktop { get; } = new() { ReachesLocalServices = true };
}

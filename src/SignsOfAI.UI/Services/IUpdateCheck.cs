namespace SignsOfAI.UI.Services;

/// <summary>
/// What a version check found: the newest published build, and whether it is newer than this one.
/// </summary>
/// <param name="Latest">The newest published version, or null when the check could not be made.</param>
/// <param name="Url">Where to read about it — the release page, never a file to download.</param>
/// <param name="IsNewer">True only when a strictly newer version exists.</param>
public sealed record UpdateStatus(string? Latest, string? Url, bool IsNewer)
{
    /// <summary>
    /// Nothing to say — the check failed, or this build is current.
    ///
    /// Failure and "you are up to date" collapse into the same answer on purpose. The interface's
    /// only correct response to a failed check is silence: the machine may be behind a school proxy
    /// or offline, and an error banner about a version check would be noise on a page about somebody
    /// else's essay.
    /// </summary>
    public static UpdateStatus Nothing { get; } = new(null, null, false);
}

/// <summary>
/// Telling the user that a newer build exists, in a host that has to be downloaded and therefore can
/// be out of date.
///
/// Two things it deliberately does not do, and both are the point:
///
/// <list type="bullet">
/// <item><b>It never downloads or runs anything.</b> The Windows build is unsigned, and a program
/// that fetches and executes code on its own is exactly the behaviour this project tells teachers to
/// be suspicious of. It reports a version and links to the release notes; the person decides.</item>
/// <item><b>It does not check until the user has said yes.</b> This is the first network call the app
/// would make without being asked, and a tool whose case rests on "nothing leaves your machine" does
/// not get to make an exception quietly — even one that sends no text and no identifier. The consent
/// is asked once, in the app, and remembered.</item>
/// </list>
///
/// A host that is always current — a browser tab, which serves whatever was last deployed — leaves
/// <see cref="IsAvailable"/> false and the interface does not mention any of this.
/// </summary>
public interface IUpdateCheck
{
    /// <summary>False where there is nothing to update: the interface offers nothing.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Asks the source of releases what the newest published build is.
    ///
    /// Never throws. No network, a proxy, a rate limit, a malformed answer — all of them return
    /// <see cref="UpdateStatus.Nothing"/>, because none of them is the user's problem and none of
    /// them is worth a message on the page.
    /// </summary>
    Task<UpdateStatus> CheckAsync(CancellationToken ct = default);
}

/// <summary>
/// The browser's answer: a page is whatever was last deployed, so it is never out of date and there
/// is nothing to check.
/// </summary>
public sealed class NoUpdateCheck : IUpdateCheck
{
    public bool IsAvailable => false;

    public Task<UpdateStatus> CheckAsync(CancellationToken ct = default) =>
        Task.FromResult(UpdateStatus.Nothing);
}

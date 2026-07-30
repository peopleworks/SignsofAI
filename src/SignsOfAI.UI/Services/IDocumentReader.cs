namespace SignsOfAI.UI.Services;

/// <summary>What a host managed to get out of a file the user picked.</summary>
/// <param name="Text">The extracted prose, ready to analyse.</param>
/// <param name="Warning">
/// A human-readable note when the file was read but something about it is worth saying — a PDF whose
/// pages are scanned images, for instance. Null when the read was clean. This is *not* the failure
/// channel: a file that could not be read at all throws, and the caller reports that.
/// </param>
public sealed record DocumentReadResult(string Text, string? Warning = null);

/// <summary>
/// Turns a picked file into text. Each host supplies its own.
///
/// This exists so the interface can offer whatever the host can actually deliver, instead of the
/// lowest common denominator. The browser build reads Word and plain text with no dependencies at
/// all — the engine has to stay small, because visitors download it. The desktop build carries a
/// PDF/ODT/EPUB/RTF reader that would be dead weight in a WebAssembly payload.
///
/// The upload control's <see cref="Accept"/> list and the hint under it both come from here, so the
/// two hosts cannot end up promising different things than they can do — the browser telling you to
/// paste your PDF is correct there and would be a lie on the desktop.
/// </summary>
public interface IDocumentReader
{
    /// <summary>Value for the file picker's <c>accept</c> attribute.</summary>
    string Accept { get; }

    /// <summary>Translation key for the line under the upload button. See <see cref="Loc"/>.</summary>
    string HintKey { get; }

    /// <summary>
    /// Reads <paramref name="stream"/> as <paramref name="fileName"/>'s format. Throws when the file
    /// cannot be read; the caller turns that into a message.
    /// </summary>
    Task<DocumentReadResult> ReadAsync(
        Stream stream, string fileName, long maxBytes, CancellationToken ct = default);
}

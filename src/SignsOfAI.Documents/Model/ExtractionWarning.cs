namespace SignsOfAI.Documents;

/// <summary>
/// A non-fatal issue encountered during extraction. The text was still retrieved but some
/// part of it may be missing or degraded. Callers should surface these in the UI so the
/// user knows, for example, that pages 3–4 were scanned images with no accessible text.
/// </summary>
/// <param name="Message">Human-readable description of the issue.</param>
/// <param name="PageNumber">
/// The page where the issue occurs, or <c>null</c> when it applies to the whole document.
/// </param>
public sealed record ExtractionWarning(string Message, int? PageNumber);

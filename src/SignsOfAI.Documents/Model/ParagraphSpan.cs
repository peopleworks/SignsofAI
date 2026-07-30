namespace SignsOfAI.Documents;

/// <summary>
/// A paragraph of extracted text with enough positional metadata that a finding can be
/// mapped back to where the reader can see it — a page number for PDFs, a paragraph index
/// for everything else.
/// </summary>
/// <param name="Index">Zero-based paragraph index within the document.</param>
/// <param name="Text">The paragraph text, without the trailing newline.</param>
/// <param name="PageNumber">
/// 1-based page number where this paragraph appears, or <c>null</c> when the source format
/// has no page concept (TXT, MD, RTF).
/// </param>
public sealed record ParagraphSpan(int Index, string Text, int? PageNumber);

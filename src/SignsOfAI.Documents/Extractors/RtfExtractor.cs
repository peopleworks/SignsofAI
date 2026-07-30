using System.Text;

namespace SignsOfAI.Documents.Extractors;

/// <summary>
/// Extracts plain text from Rich Text Format (.rtf) files with a hand-written control-word
/// stripper — no external dependency needed. Handles the most common control words found in
/// files exported by Word, WordPad, and LibreOffice.
///
/// <para>
/// RTF is a text-based format where backslash-prefixed control words toggle formatting state
/// and <c>\par</c> / <c>\line</c> mark paragraph/line breaks. This parser strips all
/// formatting, resolves hex/unicode escapes, and returns the visible prose.
/// </para>
/// </summary>
public sealed class RtfExtractor : IDocumentExtractor
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rtf",
    };

    public bool CanHandle(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName));

    public async Task<ExtractionResult> ExtractAsync(
        Stream stream,
        string fileName,
        ExtractionOptions options,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (options.MaxSizeBytes is { } max && bytes.Length > max)
        {
            return BuildFailure(fileName, ExtractionFailureReason.FileTooLarge,
                $"File is {bytes.Length:N0} bytes; limit is {max:N0} bytes.", bytes.Length);
        }

        // RTF is ASCII at its core, but may contain ANSI/Unicode characters beyond ASCII.
        // Decode as ISO-8859-1 first (every byte maps 1:1 to a char), then handle escapes.
        var raw = Encoding.GetEncoding(28591).GetString(bytes);

        if (!raw.TrimStart().StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
        {
            return BuildFailure(fileName, ExtractionFailureReason.CorruptFile,
                "This doesn't look like an RTF file (no {\\rtf header).", bytes.Length);
        }

        var text = StripRtf(raw);
        // RTF paragraphs are separated by single newlines (from \par), not blank lines.
        // Produce paragraph spans directly from the stripped text lines.
        var paragraphs = BuildParagraphs(text);

        return new ExtractionResult
        {
            Text = text,
            Paragraphs = paragraphs,
            Warnings = [],
            FileName = fileName,
            BytesRead = bytes.Length,
        };
    }

    /// <summary>
    /// State machine that strips RTF control sequences and returns plain text.
    /// Handles groups, control words, hex escapes, unicode escapes, and
    /// paragraph/line/tab markers.
    /// </summary>
    public static string StripRtf(string rtf)
    {
        var sb = new StringBuilder(rtf.Length);
        var i = 0;
        var groupDepth = 0;
        // Track whether we're inside a group whose content should be skipped
        // (e.g. {\fonttbl ...}, {\colortbl ...}, {\stylesheet ...}, {\*\...})
        var skipStack = new Stack<bool>();
        skipStack.Push(false);

        while (i < rtf.Length)
        {
            var ch = rtf[i];

            switch (ch)
            {
                case '{':
                    groupDepth++;
                    skipStack.Push(skipStack.Peek()); // inherit parent skip state
                    i++;
                    break;

                case '}':
                    if (groupDepth > 0)
                    {
                        groupDepth--;
                        skipStack.Pop();
                    }
                    i++;
                    break;

                case '\\' when i + 1 < rtf.Length:
                    i++; // skip the backslash
                    var (skip, newI) = ProcessControlWord(rtf, i, sb, skipStack);
                    i = newI;
                    // If the control word starts a destination we should skip entirely,
                    // mark the current group for skipping
                    if (skip && skipStack.Count > 0)
                    {
                        skipStack.Pop();
                        skipStack.Push(true);
                    }
                    break;

                case '\r':
                case '\n':
                    // RTF ignores bare CR/LF (they're just formatting in the source)
                    i++;
                    break;

                default:
                    if (!skipStack.Peek())
                    {
                        sb.Append(ch);
                    }
                    i++;
                    break;
            }
        }

        // Collapse multiple blank lines
        var result = sb.ToString();
        // Normalise: 3+ newlines → 2 newlines
        while (result.Contains("\n\n\n"))
            result = result.Replace("\n\n\n", "\n\n");

        return result.Trim();
    }

    /// <summary>
    /// Processes a control word, control symbol, or hex/unicode escape.
    /// Returns (shouldSkipGroup, newIndex).
    /// </summary>
    private static (bool shouldSkipGroup, int newIndex) ProcessControlWord(
        string rtf, int start, StringBuilder sb, Stack<bool> skipStack)
    {
        var shouldSkip = false;

        // Control symbol: single non-letter character
        if (start >= rtf.Length)
            return (false, start);

        var ch = rtf[start];

        // Hex escape: \'xx
        if (ch == '\'' && start + 2 < rtf.Length)
        {
            if (!skipStack.Peek())
            {
                var hex = rtf.Substring(start + 1, 2);
                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var code))
                {
                    sb.Append((char)code);
                }
            }
            return (false, start + 3);
        }

        // Unicode escape: \uNNNN? (the trailing ? is a replacement char for non-Unicode readers)
        if (ch == 'u' && start + 1 < rtf.Length && char.IsDigit(rtf[start + 1]))
        {
            var j = start + 1;
            while (j < rtf.Length && (char.IsDigit(rtf[j]) || rtf[j] == '-'))
                j++;

            if (!skipStack.Peek())
            {
                if (int.TryParse(rtf.Substring(start + 1, j - start - 1), out var unicode))
                {
                    // Clamp to valid Unicode range
                    if (unicode >= 0 && unicode <= 0x10FFFF)
                    {
                        sb.Append(char.ConvertFromUtf32(unicode));
                    }
                }
            }

            // Skip the trailing '?' replacement character if present
            if (j < rtf.Length && rtf[j] == '?')
                j++;

            // Skip any following space delimiter
            if (j < rtf.Length && rtf[j] == ' ')
                j++;

            return (false, j);
        }

        // Named control word: \word followed by a space delimiter or non-letter
        if (char.IsLetter(ch))
        {
            var j = start;
            while (j < rtf.Length && char.IsLetter(rtf[j]))
                j++;

            var word = rtf.Substring(start, j - start).ToLowerInvariant();

            // Check for destinations that should be skipped entirely
            switch (word)
            {
                case "fonttbl":
                case "colortbl":
                case "stylesheet":
                case "pict":
                case "object":
                case "info":
                case "header":
                case "footer":
                case "footnote":
                case "headerl":
                case "headerr":
                case "headerf":
                case "footerl":
                case "footerr":
                case "footerf":
                case "xml":
                    shouldSkip = true;
                    break;
            }

            // If preceded by \*, this is an ignorable destination
            // (We don't track \* here — we handle it in the calling loop by checking
            //  if the current group's skip flag was set by a prior \* check.)

            if (!shouldSkip && !skipStack.Peek())
            {
                switch (word)
                {
                    case "par":
                    case "pard":
                        sb.Append('\n');
                        break;
                    case "line":
                        sb.Append('\n');
                        break;
                    case "tab":
                        sb.Append('\t');
                        break;
                    case "lquote":
                        sb.Append('‘');
                        break;
                    case "rquote":
                        sb.Append('’');
                        break;
                    case "ldblquote":
                        sb.Append('“');
                        break;
                    case "rdblquote":
                        sb.Append('”');
                        break;
                    case "emspace":
                        sb.Append(' ');
                        break;
                    case "enspace":
                        sb.Append(' ');
                        break;
                    case "bullet":
                        sb.Append('•');
                        break;
                    case "endash":
                        sb.Append('–');
                        break;
                    case "emdash":
                        sb.Append('—');
                        break;
                }
            }

            // Skip the trailing space delimiter (RTF spec: a space after a control word
            // is a delimiter, not content)
            if (j < rtf.Length && rtf[j] == ' ')
                j++;

            return (shouldSkip, j);
        }

        // Non-letter control symbol (e.g. \{, \}, \\, \~, \-)
        if (!skipStack.Peek())
        {
            switch (ch)
            {
                case '\\': sb.Append('\\'); break;
                case '{': sb.Append('{'); break;
                case '}': sb.Append('}'); break;
                case '~': sb.Append(' '); break;  // non-breaking space
                case '-': sb.Append('­'); break;  // optional hyphen
                case '_': sb.Append('‑'); break;  // non-breaking hyphen
                case '*': shouldSkip = true; break;     // ignorable destination
                // For unknown symbols, just ignore
            }
        }
        else if (ch == '*')
        {
            shouldSkip = true;
        }

        // Skip trailing space after control symbol
        var next = start + 1;
        if (next < rtf.Length && rtf[next] == ' ')
            next++;

        return (shouldSkip, next);
    }

    /// <summary>
    /// Builds paragraph spans from RTF-extracted text, splitting on newlines
    /// (each <c>\par</c> is one paragraph) and collapsing blank lines.
    /// </summary>
    private static IReadOnlyList<ParagraphSpan> BuildParagraphs(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraphs = new List<ParagraphSpan>();
        var index = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                paragraphs.Add(new ParagraphSpan(index++, trimmed, null));
            }
        }

        return paragraphs;
    }

    private static ExtractionResult BuildFailure(
        string fileName, ExtractionFailureReason reason, string message, long bytesRead)
    {
        return new ExtractionResult
        {
            Text = "",
            Paragraphs = [],
            Warnings = [new ExtractionWarning($"[{reason}] {message}", null)],
            FileName = fileName,
            BytesRead = bytesRead,
        };
    }
}

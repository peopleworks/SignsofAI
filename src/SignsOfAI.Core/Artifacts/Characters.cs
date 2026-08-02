using System.Text;

namespace SignsOfAI.Core.Artifacts;

/// <summary>
/// The codepoint tables the scanner works from.
///
/// These stay in C# rather than in a JSON pack, which is the opposite of the rule the detection
/// catalogs follow. The reason is that a rule pack encodes an editorial judgement — whether "delve"
/// reads as AI — and reasonable people improve it by arguing. This encodes facts from the Unicode
/// standard, where being right matters more than being editable, and a pack that quietly dropped a
/// mapping would weaken a check without anyone noticing. Every string a reader ever sees still comes
/// from the packs, so translating and rewording needs no compiler.
/// </summary>
internal static class Characters
{
    /// <summary>Formats a codepoint the way the standard writes it, so it can be looked up.</summary>
    public static string Format(int codePoint) => $"U+{codePoint:X4}";

    // ---- invisible & format characters ------------------------------------------------------

    public static bool IsZeroWidth(int cp) => cp switch
    {
        0x200B => true, // ZERO WIDTH SPACE
        0x200C => true, // ZERO WIDTH NON-JOINER
        0x200D => true, // ZERO WIDTH JOINER
        0x2060 => true, // WORD JOINER
        0x2061 or 0x2062 or 0x2063 or 0x2064 => true, // invisible mathematical operators
        0xFEFF => true, // ZERO WIDTH NO-BREAK SPACE / byte-order mark
        0x180E => true, // MONGOLIAN VOWEL SEPARATOR
        0x034F => true, // COMBINING GRAPHEME JOINER
        _ => false,
    };

    public static bool IsBidiControl(int cp) =>
        cp is 0x200E or 0x200F           // left-to-right / right-to-left mark
           or (>= 0x202A and <= 0x202E)  // embedding and override
           or (>= 0x2066 and <= 0x2069); // isolates

    public static bool IsTagCharacter(int cp) => cp is >= 0xE0000 and <= 0xE007F;

    public static bool IsVariationSelector(int cp) =>
        cp is (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0100 and <= 0xE01EF);

    public static bool IsPrivateUse(int cp) =>
        cp is (>= 0xE000 and <= 0xF8FF)
           or (>= 0xF0000 and <= 0xFFFFD)
           or (>= 0x100000 and <= 0x10FFFD);

    public static bool IsUnusualSpace(int cp) => cp switch
    {
        0x00A0 => true,                    // NO-BREAK SPACE
        >= 0x2000 and <= 0x200A => true,   // en quad … hair space
        0x202F => true,                    // NARROW NO-BREAK SPACE
        0x205F => true,                    // MEDIUM MATHEMATICAL SPACE
        0x3000 => true,                    // IDEOGRAPHIC SPACE
        0x1680 => true,                    // OGHAM SPACE MARK
        _ => false,
    };

    public const int SoftHyphen = 0x00AD;

    // ---- scripts ------------------------------------------------------------------------------

    /// <summary>A letter of the Latin script, accents and all — legitimate in English and Spanish.</summary>
    public static bool IsLatinLetter(int cp) =>
        cp is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')
           or (>= 0x00C0 and <= 0x024F)   // Latin-1 Supplement and Latin Extended-A/B
           or (>= 0x1E00 and <= 0x1EFF);  // Latin Extended Additional

    /// <summary>
    /// Scripts where a zero-width joiner or non-joiner is ordinary orthography rather than an
    /// artifact — Arabic, Hebrew, the Indic scripts, Myanmar. Flagging those would be a bug.
    /// </summary>
    public static bool NeedsJoinControls(int cp) =>
        cp is (>= 0x0590 and <= 0x08FF)    // Hebrew, Arabic, Syriac, Thaana, N'Ko, Arabic Extended
           or (>= 0x0900 and <= 0x0DFF)    // Devanagari … Sinhala
           or (>= 0x1000 and <= 0x109F)    // Myanmar
           or (>= 0xFB1D and <= 0xFDFF)    // Hebrew/Arabic presentation forms
           or (>= 0xFE70 and <= 0xFEFC);   // Arabic presentation forms-B

    /// <summary>
    /// Roughly "this could be part of an emoji". Used only to decide whether a joiner or a variation
    /// selector next to it is legitimate, so over-inclusion here costs a missed artifact, never a
    /// false accusation — which is the direction to err in.
    /// </summary>
    public static bool IsEmojiLike(int cp) =>
        cp is (>= 0x1F000 and <= 0x1FAFF)
           or (>= 0x2190 and <= 0x2BFF)
           or 0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139
           or 0x3030 or 0x303D or 0x3297 or 0x3299
           or (>= 0xFE00 and <= 0xFE0F);

    /// <summary>Blocks that hold styled Latin letters: mathematical alphanumerics and fullwidth forms.</summary>
    private static bool IsStyledLatinBlock(int cp) =>
        cp is (>= 0x1D400 and <= 0x1D7FF)  // Mathematical Alphanumeric Symbols
           or (>= 0xFF21 and <= 0xFF3A)    // FULLWIDTH LATIN CAPITAL A–Z
           or (>= 0xFF41 and <= 0xFF5A);   // FULLWIDTH LATIN SMALL A–Z

    // ---- lookalikes ----------------------------------------------------------------------------

    /// <summary>
    /// Non-Latin letters that read as Latin ones. Cyrillic and Greek carry the weight because that is
    /// what the substitution tools reach for.
    ///
    /// What is deliberately absent: every accented Latin letter. "á", "ñ" and "ü" are ordinary
    /// Spanish, and a table that treated them as impostors would turn this check into exactly the
    /// kind of instrument that punishes people for the language they write in.
    /// </summary>
    private static readonly Dictionary<int, char> Table = new()
    {
        // Cyrillic, lower case
        [0x0430] = 'a', [0x0432] = 'b', [0x0435] = 'e', [0x043A] = 'k', [0x043C] = 'm',
        [0x043D] = 'h', [0x043E] = 'o', [0x0440] = 'p', [0x0441] = 'c', [0x0442] = 't',
        [0x0443] = 'y', [0x0445] = 'x', [0x0455] = 's', [0x0456] = 'i', [0x0458] = 'j',
        [0x0475] = 'v', [0x04BB] = 'h', [0x0501] = 'd', [0x051B] = 'q', [0x051D] = 'w',
        // Cyrillic, upper case
        [0x0410] = 'A', [0x0412] = 'B', [0x0415] = 'E', [0x041A] = 'K', [0x041C] = 'M',
        [0x041D] = 'H', [0x041E] = 'O', [0x0420] = 'P', [0x0421] = 'C', [0x0422] = 'T',
        [0x0423] = 'Y', [0x0425] = 'X', [0x0405] = 'S', [0x0406] = 'I', [0x0408] = 'J',
        [0x0500] = 'D', [0x051A] = 'Q', [0x051C] = 'W',
        // Greek, lower case
        [0x03B1] = 'a', [0x03B3] = 'y', [0x03B5] = 'e', [0x03B9] = 'i', [0x03BA] = 'k',
        [0x03BD] = 'v', [0x03BF] = 'o', [0x03C1] = 'p', [0x03C4] = 't', [0x03C5] = 'u',
        [0x03C7] = 'x', [0x03C9] = 'w', [0x03F2] = 'c', [0x03F3] = 'j',
        // Greek, upper case
        [0x0391] = 'A', [0x0392] = 'B', [0x0395] = 'E', [0x0396] = 'Z', [0x0397] = 'H',
        [0x0399] = 'I', [0x039A] = 'K', [0x039C] = 'M', [0x039D] = 'N', [0x039F] = 'O',
        [0x03A1] = 'P', [0x03A4] = 'T', [0x03A5] = 'Y', [0x03A7] = 'X',
        // Armenian
        [0x0570] = 'h', [0x0578] = 'n', [0x057D] = 'u', [0x0585] = 'o',
        // Latin letters that are not the ASCII one they look like
        [0x0131] = 'i', // DOTLESS I
        [0x0251] = 'a', // LATIN SMALL LETTER ALPHA
        [0x0261] = 'g', // LATIN SMALL LETTER SCRIPT G
        [0x0269] = 'i', // LATIN SMALL LETTER IOTA
    };

    /// <summary>
    /// The Latin letter this codepoint impersonates, or null when it impersonates nothing.
    ///
    /// Styled Latin — mathematical bold, fullwidth — resolves through NFKC rather than a table,
    /// because the standard already defines that mapping and restating it by hand would only
    /// introduce errors.
    /// </summary>
    public static char? LooksLike(int cp)
    {
        if (Table.TryGetValue(cp, out var latin))
            return latin;

        if (!IsStyledLatinBlock(cp))
            return null;

        var folded = char.ConvertFromUtf32(cp).Normalize(NormalizationForm.FormKC);
        return folded.Length == 1 && folded[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')
            ? folded[0]
            : null;
    }

    // ---- names ---------------------------------------------------------------------------------

    private static readonly Dictionary<int, string> Names = new()
    {
        [0x200B] = "ZERO WIDTH SPACE",
        [0x200C] = "ZERO WIDTH NON-JOINER",
        [0x200D] = "ZERO WIDTH JOINER",
        [0x200E] = "LEFT-TO-RIGHT MARK",
        [0x200F] = "RIGHT-TO-LEFT MARK",
        [0x2060] = "WORD JOINER",
        [0xFEFF] = "ZERO WIDTH NO-BREAK SPACE",
        [0x180E] = "MONGOLIAN VOWEL SEPARATOR",
        [0x034F] = "COMBINING GRAPHEME JOINER",
        [0x00A0] = "NO-BREAK SPACE",
        [0x00AD] = "SOFT HYPHEN",
        [0x202F] = "NARROW NO-BREAK SPACE",
        [0x205F] = "MEDIUM MATHEMATICAL SPACE",
        [0x3000] = "IDEOGRAPHIC SPACE",
        [0x1680] = "OGHAM SPACE MARK",
        [0x2002] = "EN SPACE",
        [0x2003] = "EM SPACE",
        [0x2009] = "THIN SPACE",
        [0x200A] = "HAIR SPACE",
    };

    /// <summary>
    /// A name for the codepoint. Known ones are named exactly; the rest get their block, which is
    /// still enough for a reader to look the character up and check us.
    /// </summary>
    public static string Name(int cp)
    {
        if (Names.TryGetValue(cp, out var name))
            return name;

        return cp switch
        {
            >= 0x0400 and <= 0x04FF => "CYRILLIC LETTER",
            >= 0x0500 and <= 0x052F => "CYRILLIC SUPPLEMENT LETTER",
            >= 0x0370 and <= 0x03FF => "GREEK LETTER",
            >= 0x0530 and <= 0x058F => "ARMENIAN LETTER",
            >= 0x1D400 and <= 0x1D7FF => "MATHEMATICAL ALPHANUMERIC SYMBOL",
            >= 0xFF01 and <= 0xFF5E => "FULLWIDTH FORM",
            >= 0x2000 and <= 0x200A => "SPACE",
            >= 0x202A and <= 0x202E => "BIDIRECTIONAL FORMATTING",
            >= 0x2066 and <= 0x2069 => "BIDIRECTIONAL ISOLATE",
            >= 0xE0000 and <= 0xE007F => "TAG CHARACTER",
            >= 0xFE00 and <= 0xFE0F => "VARIATION SELECTOR",
            >= 0xE0100 and <= 0xE01EF => "VARIATION SELECTOR SUPPLEMENT",
            _ => "PRIVATE USE OR UNNAMED",
        };
    }
}
